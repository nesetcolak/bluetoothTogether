using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text.Json;
using System.IO;

namespace bluetoothTogetheForms
{
    public partial class Form1 : Form
    {
        private WasapiLoopbackCapture capture;
        private List<WasapiOut> outputs = new List<WasapiOut>();
        private List<BufferedWaveProvider> buffers = new List<BufferedWaveProvider>();
        private List<MMDevice> availableDevices = new List<MMDevice>();

        private MMDeviceEnumerator enumerator;
        private CoreAudioController audioController;

        private MMDevice virtualCableDevice;
        private string originalDeviceId = "";

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private bool gercektenKapat = false;

        private System.Windows.Forms.Timer volumeSyncTimer;
        private float lastVolume = -1f;
        private bool lastMute = false;

        private Button btnReset;
        private Panel pnlDeviceCards;
        private Panel pnlStatusBar;
        private Label lblStatusDot;
        private Label lblStatusText;
        private Panel pnlSignal;
        private System.Windows.Forms.Timer signalTimer;
        private int signalFrame = 0;
        private bool isRunning = false;

        // Delay ayarları: cihaz adı → ms
        private Dictionary<string, int> deviceDelays = new Dictionary<string, int>();
        private string settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BluetoothTogether", "settings.json");

        private readonly Color BG_DEEP = Color.FromArgb(13, 13, 15);
        private readonly Color BG_CARD = Color.FromArgb(22, 22, 26);
        private readonly Color BG_SURFACE = Color.FromArgb(30, 30, 36);
        private readonly Color ACCENT = Color.FromArgb(0, 212, 255);
        private readonly Color ACCENT2 = Color.FromArgb(0, 255, 180);
        private readonly Color DANGER = Color.FromArgb(255, 60, 80);
        private readonly Color TEXT_PRI = Color.FromArgb(240, 240, 245);
        private readonly Color TEXT_SEC = Color.FromArgb(120, 120, 135);
        private readonly Color BORDER = Color.FromArgb(45, 45, 55);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_MICA_EFFECT = 1029;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_NONE = 1;

        private Panel customTitleBar;
        private Point _dragStart;
        private bool _dragging;

        public Form1()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;

            int darkMode = 1;
            DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            int noBackdrop = DWMSBT_NONE;
            DwmSetWindowAttribute(this.Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref noBackdrop, sizeof(int));
            int micaOff = 0;
            DwmSetWindowAttribute(this.Handle, DWMWA_MICA_EFFECT, ref micaOff, sizeof(int));

            LoadSettings();
            BuildUI();
        }

        // ══════════════════════════════════════════════════
        // SETTINGS
        // ══════════════════════════════════════════════════
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    deviceDelays = JsonSerializer.Deserialize<Dictionary<string, int>>(json)
                                   ?? new Dictionary<string, int>();
                }
            }
            catch { deviceDelays = new Dictionary<string, int>(); }
        }

        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(settingsPath, JsonSerializer.Serialize(deviceDelays));
            }
            catch { }
        }

        private int GetDelay(string deviceName)
        {
            return deviceDelays.TryGetValue(deviceName, out int d) ? d : 0;
        }

        private void SetDelay(string deviceName, int ms)
        {
            deviceDelays[deviceName] = ms;
            SaveSettings();
        }

        // ══════════════════════════════════════════════════
        // DRAG
        // ══════════════════════════════════════════════════
        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragStart = e.Location;
                if (sender is Control c) _dragStart = c.PointToScreen(e.Location);
                _dragStart = new Point(_dragStart.X - this.Left, _dragStart.Y - this.Top);
            }
        }
        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                var cur = Control.MousePosition;
                this.Location = new Point(cur.X - _dragStart.X, cur.Y - _dragStart.Y);
            }
        }
        private void TitleBar_MouseUp(object sender, MouseEventArgs e) => _dragging = false;

        // ══════════════════════════════════════════════════
        // UI BUILDER
        // ══════════════════════════════════════════════════
        private void BuildUI()
        {
            lblStatus.Visible = false;
            clbDevices.Visible = false;
            btnStart.Visible = false;
            btnStop.Visible = false;

            this.BackColor = BG_DEEP;
            this.ForeColor = TEXT_PRI;
            this.Size = new Size(420, 540);
            this.MinimumSize = new Size(420, 480);
            this.Text = "BluetoothTogether";
            this.Font = new Font("Segoe UI", 9.5F);
            this.Padding = new Padding(0);

            // ── CUSTOM TITLEBAR ───────────────────────────
            customTitleBar = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = BG_DEEP };
            customTitleBar.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(35, 35, 42), 1);
                e.Graphics.DrawLine(pen, 0, customTitleBar.Height - 1, customTitleBar.Width, customTitleBar.Height - 1);
            };

            var picIcon = new PictureBox { Size = new Size(16, 16), Location = new Point(10, 8), BackColor = Color.Transparent, SizeMode = PictureBoxSizeMode.StretchImage };
            try { var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); if (exeIcon != null) picIcon.Image = exeIcon.ToBitmap(); }
            catch { try { string p = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "app_icon.ico"); if (File.Exists(p)) picIcon.Image = new Icon(p, 16, 16).ToBitmap(); } catch { } }

            var lblTitleBar = new Label { Text = "BluetoothTogether", Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(160, 160, 170), AutoSize = true, Location = new Point(32, 8), BackColor = Color.Transparent };

            var pnlWinBtns = new Panel { Dock = DockStyle.Right, Width = 92, BackColor = Color.Transparent };

            var btnMinimize = new Button { Text = "─", Size = new Size(46, 32), Location = new Point(0, 0), FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Color.FromArgb(160, 160, 170), Font = new Font("Segoe UI", 10F), Cursor = Cursors.Hand };
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 58);
            btnMinimize.MouseEnter += (s, e) => btnMinimize.ForeColor = Color.White;
            btnMinimize.MouseLeave += (s, e) => btnMinimize.ForeColor = Color.FromArgb(160, 160, 170);
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            var btnClose = new Button { Text = "✕", Size = new Size(46, 32), Location = new Point(46, 0), FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Color.FromArgb(160, 160, 170), Font = new Font("Segoe UI", 10F), Cursor = Cursors.Hand };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(196, 43, 28);
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.FromArgb(160, 160, 170);
            btnClose.Click += (s, e) => Form1_FormClosing_Manual();

            pnlWinBtns.Controls.Add(btnMinimize);
            pnlWinBtns.Controls.Add(btnClose);
            customTitleBar.Controls.Add(pnlWinBtns);
            customTitleBar.Controls.Add(picIcon);
            customTitleBar.Controls.Add(lblTitleBar);
            customTitleBar.MouseDown += TitleBar_MouseDown;
            customTitleBar.MouseMove += TitleBar_MouseMove;
            customTitleBar.MouseUp += TitleBar_MouseUp;
            lblTitleBar.MouseDown += TitleBar_MouseDown;
            lblTitleBar.MouseMove += TitleBar_MouseMove;
            lblTitleBar.MouseUp += TitleBar_MouseUp;
            picIcon.MouseDown += TitleBar_MouseDown;
            picIcon.MouseMove += TitleBar_MouseMove;
            picIcon.MouseUp += TitleBar_MouseUp;

            // ── HEADER ───────────────────────────────────
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = BG_CARD };
            pnlHeader.Paint += (s, e) =>
            {
                using var pen = new Pen(BORDER, 1);
                e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                using var gb = new LinearGradientBrush(new Point(0, 10), new Point(0, 66), Color.FromArgb(0, ACCENT), ACCENT);
                e.Graphics.FillRectangle(gb, 0, 10, 3, 56);
            };
            var lblTitle = new Label { Text = "BLUETOOTH TOGETHER", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = TEXT_PRI, AutoSize = true, Location = new Point(22, 14) };
            var lblSub = new Label { Text = "Multi-device audio router", Font = new Font("Segoe UI", 9F), ForeColor = TEXT_SEC, AutoSize = true, Location = new Point(23, 40) };
            pnlSignal = new Panel { Size = new Size(52, 28), BackColor = Color.Transparent, Location = new Point(348, 26) };
            pnlSignal.Paint += PnlSignal_Paint;
            signalTimer = new System.Windows.Forms.Timer { Interval = 110 };
            signalTimer.Tick += (s, e) => { signalFrame = (signalFrame + 1) % 8; pnlSignal.Invalidate(); };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSub);
            pnlHeader.Controls.Add(pnlSignal);

            // ── STATUS BAR ───────────────────────────────
            pnlStatusBar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = BG_SURFACE };
            pnlStatusBar.Paint += (s, e) => { using var pen = new Pen(BORDER, 1); e.Graphics.DrawLine(pen, 0, pnlStatusBar.Height - 1, pnlStatusBar.Width, pnlStatusBar.Height - 1); };
            lblStatusDot = new Label { Text = "●", Font = new Font("Segoe UI", 9F), ForeColor = TEXT_SEC, AutoSize = true, Location = new Point(22, 10) };
            lblStatusText = new Label { Text = "Initializing...", Font = new Font("Segoe UI", 9F), ForeColor = TEXT_SEC, AutoSize = true, Location = new Point(40, 10) };
            pnlStatusBar.Controls.Add(lblStatusDot);
            pnlStatusBar.Controls.Add(lblStatusText);

            // ── DEVICE LABEL ─────────────────────────────
            var pnlDevLabel = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent };
            var lblDev = new Label { Text = "OUTPUT DEVICES", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = TEXT_SEC, AutoSize = true, Location = new Point(22, 13) };
            pnlDevLabel.Controls.Add(lblDev);

            // ── DEVICE CARDS ─────────────────────────────
            pnlDeviceCards = new Panel { Dock = DockStyle.Top, Height = 220, BackColor = Color.Transparent, AutoScroll = true, Padding = new Padding(14, 0, 14, 0) };

            // ── BUTTON ROW ───────────────────────────────
            var pnlBtns = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = BG_CARD };
            pnlBtns.Paint += (s, e) => { using var pen = new Pen(BORDER, 1); e.Graphics.DrawLine(pen, 0, 0, pnlBtns.Width, 0); };

            var btnStartNew = MakeButton("▶  START", ACCENT, BG_DEEP, true);
            btnStartNew.Size = new Size(156, 44); btnStartNew.Location = new Point(14, 16);
            btnStartNew.Click += (s, e) => BtnStartNew_Click();

            var btnStopNew = MakeButton("■  STOP", BG_SURFACE, TEXT_SEC, false);
            btnStopNew.Size = new Size(118, 44); btnStopNew.Location = new Point(178, 16);
            btnStopNew.Enabled = false;
            btnStopNew.Click += (s, e) => StopAudioRouting();

            btnReset = MakeButton("↺", BG_SURFACE, TEXT_SEC, false);
            btnReset.Size = new Size(44, 44); btnReset.Location = new Point(304, 16);
            btnReset.Font = new Font("Segoe UI", 14F);
            btnReset.Click += BtnReset_Click;

            btnStart.Tag = btnStartNew;
            btnStop.Tag = btnStopNew;

            pnlBtns.Controls.Add(btnStartNew);
            pnlBtns.Controls.Add(btnStopNew);
            pnlBtns.Controls.Add(btnReset);

            // ── ASSEMBLE ─────────────────────────────────
            this.Controls.Clear();
            this.Controls.Add(lblStatus);
            this.Controls.Add(clbDevices);
            this.Controls.Add(btnStart);
            this.Controls.Add(btnStop);
            this.Controls.Add(pnlBtns);
            this.Controls.Add(pnlDeviceCards);
            this.Controls.Add(pnlDevLabel);
            this.Controls.Add(pnlStatusBar);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(customTitleBar);

            // ── TRAY ─────────────────────────────────────
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Show", null, OnShowClick);
            trayMenu.Items.Add("Exit", null, OnExitClick);
            trayIcon = new NotifyIcon { Text = "BluetoothTogether", Icon = this.Icon, ContextMenuStrip = trayMenu, Visible = true };
            trayIcon.DoubleClick += TrayIcon_DoubleClick;

            // ── VOLUME SYNC TIMER ────────────────────────
            volumeSyncTimer = new System.Windows.Forms.Timer { Interval = 250 };
            volumeSyncTimer.Tick += VolumeSyncTimer_Tick;
        }

        private void PnlSignal_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int[] heights = { 4, 8, 14, 20, 14, 8, 4, 10 };
            int barW = 4, gap = 3;
            for (int i = 0; i < 6; i++)
            {
                int h = isRunning ? heights[(signalFrame + i) % heights.Length] : 4;
                var color = isRunning ? Color.FromArgb(Math.Min(255, 80 + i * 30), ACCENT2) : Color.FromArgb(40, TEXT_SEC);
                using var brush = new SolidBrush(color);
                e.Graphics.FillRectangle(brush, i * (barW + gap), (24 - h) / 2, barW, h);
            }
        }

        private Button MakeButton(string text, Color back, Color fore, bool isAccent)
        {
            var btn = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = fore, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter };
            btn.FlatAppearance.BorderSize = isAccent ? 0 : 1;
            btn.FlatAppearance.BorderColor = BORDER;
            btn.FlatAppearance.MouseOverBackColor = isAccent ? Color.FromArgb(0, 190, 230) : Color.FromArgb(40, 40, 50);
            return btn;
        }

        // ══════════════════════════════════════════════════
        // DEVICE CARDS
        // ══════════════════════════════════════════════════
        private void RebuildDeviceCards()
        {
            pnlDeviceCards.Controls.Clear();
            clbDevices.Items.Clear();
            int y = 6;

            for (int i = 0; i < availableDevices.Count; i++)
            {
                var dev = availableDevices[i];
                bool autoChecked =
                    dev.FriendlyName.IndexOf("Headphone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dev.FriendlyName.IndexOf("Bluetooth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dev.FriendlyName.IndexOf("Stereo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dev.FriendlyName.IndexOf("Kulaklık", StringComparison.OrdinalIgnoreCase) >= 0;

                clbDevices.Items.Add(dev.FriendlyName);
                if (autoChecked) clbDevices.SetItemChecked(i, true);

                var card = BuildDeviceCard(dev.FriendlyName, autoChecked, i);
                card.Location = new Point(0, y);
                card.Width = pnlDeviceCards.ClientSize.Width - 4;
                card.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                pnlDeviceCards.Controls.Add(card);
                y += card.Height + 6;
            }

            pnlDeviceCards.Height = Math.Max(90, y + 6);
            this.Height = Math.Max(420, Math.Min(680, 96 + 36 + 34 + pnlDeviceCards.Height + 76 + 24));
        }

        private Panel BuildDeviceCard(string name, bool isChecked, int index)
        {
            int savedDelay = GetDelay(name);

            // Kart yüksekliği: normal 52 + delay satırı 36 = 88
            var card = new Panel { Height = 88, BackColor = BG_CARD, Cursor = Cursors.Default, Tag = isChecked };

            card.Paint += (s, e) =>
            {
                bool sel = (bool)card.Tag;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(sel ? Color.FromArgb(55, ACCENT) : BORDER, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                if (sel)
                {
                    using var gb = new LinearGradientBrush(new Point(0, 8), new Point(0, card.Height - 8), Color.FromArgb(0, ACCENT), ACCENT);
                    e.Graphics.FillRectangle(gb, 0, 8, 3, card.Height - 16);
                }
                // Ayırıcı çizgi (üst kısım / delay kısım)
                using var divPen = new Pen(Color.FromArgb(35, 35, 45), 1);
                e.Graphics.DrawLine(divPen, 14, 56, card.Width - 14, 56);
            };

            // Checkbox
            var chk = new Label { Size = new Size(22, 22), Location = new Point(14, 15), BackColor = Color.Transparent, ForeColor = isChecked ? ACCENT : TEXT_SEC, Text = isChecked ? "◉" : "○", Font = new Font("Segoe UI", 12F), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };

            // Cihaz adı
            string shortName = name.Length > 44 ? name.Substring(0, 42) + "…" : name;
            var lblN = new Label { Text = shortName, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = isChecked ? TEXT_PRI : TEXT_SEC, AutoSize = false, Size = new Size(card.Width - 68, 20), Location = new Point(44, 8), BackColor = Color.Transparent, Cursor = Cursors.Hand, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            var lblT = new Label { Text = GetDeviceTypeLabel(name), Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(75, 75, 90), AutoSize = true, Location = new Point(44, 28), BackColor = Color.Transparent, Cursor = Cursors.Hand };

            // ── DELAY SATIRI ─────────────────────────────
            var lblDelay = new Label { Text = "Delay", Font = new Font("Segoe UI", 8F), ForeColor = TEXT_SEC, AutoSize = true, Location = new Point(14, 64) };

            // Custom dark slider (TrackBar yerine — tema uyumsuzluğunu önler)
            int sliderValue = savedDelay;
            bool sliderDragging = false;

            var sliderTrack = new Panel
            {
                Size = new Size(card.Width - 130, 24),
                Location = new Point(52, 62),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var lblMs = new Label
            {
                Text = savedDelay == 0 ? "0 ms" : $"{savedDelay} ms",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = savedDelay == 0 ? TEXT_SEC : ACCENT,
                AutoSize = true,
                Location = new Point(card.Width - 72, 64),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                BackColor = Color.Transparent
            };

            void UpdateSliderValue(int x)
            {
                int w = sliderTrack.Width - 12;
                sliderValue = Math.Max(0, Math.Min(500, (int)((float)x / w * 500)));
                sliderTrack.Invalidate();
                lblMs.Text = sliderValue == 0 ? "0 ms" : $"{sliderValue} ms";
                lblMs.ForeColor = sliderValue == 0 ? TEXT_SEC : ACCENT;
                SetDelay(name, sliderValue);
            }

            sliderTrack.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int tw = sliderTrack.Width - 12;
                int cy = sliderTrack.Height / 2;

                // Track arka plan
                using var trackBrush = new SolidBrush(Color.FromArgb(45, 45, 55));
                g.FillRoundedRectangle(trackBrush, 4, cy - 2, tw, 4, 2);

                // Dolu kısım
                int fillW = (int)((float)sliderValue / 500 * tw);
                if (fillW > 0)
                {
                    using var fillBrush = new SolidBrush(sliderValue == 0 ? TEXT_SEC : ACCENT);
                    g.FillRoundedRectangle(fillBrush, 4, cy - 2, fillW, 4, 2);
                }

                // Thumb
                int tx = 4 + fillW;
                using var thumbBrush = new SolidBrush(sliderValue == 0 ? Color.FromArgb(80, 80, 95) : ACCENT);
                g.FillEllipse(thumbBrush, tx - 5, cy - 5, 10, 10);
            };

            sliderTrack.MouseDown += (s, e) => { sliderDragging = true; UpdateSliderValue(e.X); };
            sliderTrack.MouseMove += (s, e) => { if (sliderDragging) UpdateSliderValue(e.X); };
            sliderTrack.MouseUp += (s, e) => { sliderDragging = false; };

            // Toggle fonksiyonu
            void Toggle()
            {
                bool ns = !(bool)card.Tag;
                card.Tag = ns;
                chk.Text = ns ? "◉" : "○";
                chk.ForeColor = ns ? ACCENT : TEXT_SEC;
                lblN.ForeColor = ns ? TEXT_PRI : TEXT_SEC;
                card.Invalidate();
                if (index < clbDevices.Items.Count) clbDevices.SetItemChecked(index, ns);
            }

            card.Click += (s, e) => { var pt = card.PointToClient(Control.MousePosition); if (pt.Y < 56) Toggle(); };
            chk.Click += (s, e) => Toggle();
            lblN.Click += (s, e) => Toggle();
            lblT.Click += (s, e) => Toggle();

            card.Controls.Add(chk);
            card.Controls.Add(lblN);
            card.Controls.Add(lblT);
            card.Controls.Add(lblDelay);
            card.Controls.Add(sliderTrack);
            card.Controls.Add(lblMs);
            return card;
        }

        private string GetDeviceTypeLabel(string name)
        {
            if (name.IndexOf("Bluetooth", StringComparison.OrdinalIgnoreCase) >= 0) return "BLUETOOTH";
            if (name.IndexOf("Headphone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Kulaklık", StringComparison.OrdinalIgnoreCase) >= 0) return "HEADPHONES";
            if (name.IndexOf("Speaker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Hoparlör", StringComparison.OrdinalIgnoreCase) >= 0) return "SPEAKER";
            if (name.IndexOf("Realtek", StringComparison.OrdinalIgnoreCase) >= 0) return "ONBOARD AUDIO";
            if (name.IndexOf("HDMI", StringComparison.OrdinalIgnoreCase) >= 0) return "HDMI";
            return "AUDIO DEVICE";
        }

        private void SetStatus(string text, Color dot)
        {
            lblStatusDot.ForeColor = dot;
            lblStatusText.Text = text;
            lblStatusText.ForeColor = dot == TEXT_SEC ? TEXT_SEC : TEXT_PRI;
        }

        private Button GetStartBtn() => btnStart.Tag as Button;
        private Button GetStopBtn() => btnStop.Tag as Button;

        // ══════════════════════════════════════════════════
        // LOAD & INIT
        // ══════════════════════════════════════════════════
        private async void Form1_Load(object sender, EventArgs e)
        {
            var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
            this.Location = new Point(
                screen.Left + (screen.Width - this.Width) / 2,
                screen.Top + (screen.Height - this.Height) / 2);

            if (!IsVirtualCableInstalled()) { ShowVirtualCableInstallPrompt(); return; }
            await InitializeDevicesAsync();
        }

        private bool IsVirtualCableInstalled()
        {
            try
            {
                var tmp = new MMDeviceEnumerator();
                var all = tmp.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
                return all.Any(d =>
                    d.FriendlyName.IndexOf("VB-Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.FriendlyName.IndexOf("CABLE Input", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.FriendlyName.IndexOf("bluetoothTogether", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch { return false; }
        }

        private void ShowVirtualCableInstallPrompt()
        {
            pnlDeviceCards.Controls.Clear();
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var ico = new Label { Text = "⚡", Font = new Font("Segoe UI", 32F), ForeColor = ACCENT, AutoSize = true, Location = new Point(168, 8) };
            var lblT = new Label { Text = "Virtual Audio Driver Required", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = TEXT_PRI, AutoSize = false, Size = new Size(360, 24), Location = new Point(0, 68), TextAlign = ContentAlignment.MiddleCenter };
            var lblI = new Label { Text = "VB-Audio Virtual Cable is free.\nInstallation takes 1 minute. Restart the app after installing.", Font = new Font("Segoe UI", 9F), ForeColor = TEXT_SEC, AutoSize = false, Size = new Size(360, 44), Location = new Point(0, 100), TextAlign = ContentAlignment.MiddleCenter };
            var bDL = MakeButton("⬇   Download VB-Audio  —  vb-audio.com", ACCENT, BG_DEEP, true);
            bDL.Size = new Size(360, 44); bDL.Location = new Point(0, 158);
            bDL.Click += (s, ev) => Process.Start(new ProcessStartInfo { FileName = "https://vb-audio.com/Cable/", UseShellExecute = true });
            var bChk = MakeButton("✓   Installed, Check Again", BG_SURFACE, TEXT_PRI, false);
            bChk.Size = new Size(360, 40); bChk.Location = new Point(0, 210);
            bChk.FlatAppearance.BorderColor = BORDER;
            bChk.Click += async (s, ev) =>
            {
                if (IsVirtualCableInstalled()) { pnl.Visible = false; await InitializeDevicesAsync(); }
                else { SetStatus("VB-Audio not found", DANGER); MessageBox.Show("VB-Audio is still not found.", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            pnl.Controls.Add(ico); pnl.Controls.Add(lblT); pnl.Controls.Add(lblI);
            pnl.Controls.Add(bDL); pnl.Controls.Add(bChk);
            pnlDeviceCards.Controls.Add(pnl);
            pnlDeviceCards.Height = 270;
        }

        private async Task InitializeDevicesAsync()
        {
            GetStartBtn().Enabled = false;
            btnReset.Enabled = false;
            SetStatus("Initializing system...", Color.FromArgb(255, 165, 0));
            await Task.Run(() => { enumerator = new MMDeviceEnumerator(); audioController = new CoreAudioController(); });
            ScanDevices();
            if (virtualCableDevice == null) { SetStatus("Error: Virtual cable not found", DANGER); ShowRestartDialog("An unexpected error occurred.\nPlease restart the application."); return; }
            GetStartBtn().Enabled = true;
            btnReset.Enabled = true;
            SetStatus("Ready", ACCENT2);
        }

        private void ScanDevices()
        {
            clbDevices.Items.Clear();
            var all = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
            virtualCableDevice = all.FirstOrDefault(d =>
                d.FriendlyName.IndexOf("bluetoothTogether", StringComparison.OrdinalIgnoreCase) >= 0 ||
                d.FriendlyName.IndexOf("VB-Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                d.FriendlyName.IndexOf("CABLE Input", StringComparison.OrdinalIgnoreCase) >= 0);
            availableDevices = all.Where(d => d.ID != virtualCableDevice?.ID).ToList();
            RebuildDeviceCards();
        }

        // ══════════════════════════════════════════════════
        // START
        // ══════════════════════════════════════════════════
        private void BtnStartNew_Click()
        {
            if (clbDevices.CheckedIndices.Count == 0) { MessageBox.Show("Please select at least one device.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            try
            {
                var cur = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (cur != null && cur.ID != virtualCableDevice.ID) originalDeviceId = cur.ID;

                var cable = audioController.GetDevices().FirstOrDefault(d => d.RealId == virtualCableDevice.ID);
                cable?.SetAsDefault(); cable?.SetAsDefaultCommunications();

                capture = new WasapiLoopbackCapture(virtualCableDevice);

                foreach (int idx in clbDevices.CheckedIndices)
                {
                    var device = availableDevices[idx];
                    int delayMs = GetDelay(device.FriendlyName);
                    var buf = new BufferedWaveProvider(capture.WaveFormat) { DiscardOnBufferOverflow = true };

                    // Delay için buffer'a önceden boş veri ekle
                    if (delayMs > 0)
                    {
                        int silenceSamples = (int)(capture.WaveFormat.SampleRate * (delayMs / 1000.0)) * capture.WaveFormat.BlockAlign;
                        buf.AddSamples(new byte[silenceSamples], 0, silenceSamples);
                    }

                    var out_ = new WasapiOut(device, AudioClientShareMode.Shared, true, 20);
                    out_.Init(buf); out_.Play();
                    buffers.Add(buf); outputs.Add(out_);
                }

                capture.DataAvailable += (s, a) => { foreach (var b in buffers) b.AddSamples(a.Buffer, 0, a.BytesRecorded); };
                capture.StartRecording();

                lastVolume = -1f; lastMute = false;
                volumeSyncTimer.Start();
                isRunning = true;
                signalTimer.Start();

                SetStatus($"Routing to {clbDevices.CheckedIndices.Count} device(s)", ACCENT2);

                var sb = GetStartBtn(); var stb = GetStopBtn();
                sb.Enabled = false; sb.BackColor = BG_SURFACE;
                stb.Enabled = true; stb.BackColor = DANGER; stb.ForeColor = Color.White;
                stb.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 40, 60);
                btnReset.Enabled = false;
                SetDeviceCardsEnabled(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                StopAudioRouting();
            }
        }

        private void SetDeviceCardsEnabled(bool en)
        {
            foreach (Control c in pnlDeviceCards.Controls) c.Enabled = en;
        }

        // ══════════════════════════════════════════════════
        // RESET
        // ══════════════════════════════════════════════════
        private void BtnReset_Click(object sender, EventArgs e)
        {
            bool wasRunning = (capture != null);
            if (wasRunning) StopAudioRouting();
            SetStatus("Scanning devices...", Color.FromArgb(255, 165, 0));
            ScanDevices();
            SetStatus($"{availableDevices.Count} device(s) found", ACCENT2);
            if (wasRunning) MessageBox.Show("Audio stopped. Press START to begin again.", "Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ══════════════════════════════════════════════════
        // VOLUME SYNC
        // ══════════════════════════════════════════════════
        private void VolumeSyncTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (virtualCableDevice == null) return;
                float vol = virtualCableDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
                bool mute = virtualCableDevice.AudioEndpointVolume.Mute;
                if (Math.Abs(vol - lastVolume) < 0.001f && mute == lastMute) return;
                lastVolume = vol; lastMute = mute;
                foreach (int idx in clbDevices.CheckedIndices)
                {
                    try { availableDevices[idx].AudioEndpointVolume.MasterVolumeLevelScalar = vol; availableDevices[idx].AudioEndpointVolume.Mute = mute; } catch { }
                }
            }
            catch { }
        }

        // ══════════════════════════════════════════════════
        // STOP
        // ══════════════════════════════════════════════════
        private void StopAudioRouting()
        {
            volumeSyncTimer?.Stop(); signalTimer?.Stop();
            isRunning = false; pnlSignal?.Invalidate();
            if (capture != null) { capture.StopRecording(); capture.Dispose(); capture = null; }
            foreach (var o in outputs) { try { o?.Stop(); o?.Dispose(); } catch { } }
            outputs.Clear(); buffers.Clear();
            if (!string.IsNullOrEmpty(originalDeviceId))
            {
                var orig = audioController.GetDevices().FirstOrDefault(d => d.RealId == originalDeviceId);
                orig?.SetAsDefault(); orig?.SetAsDefaultCommunications();
                originalDeviceId = "";
            }
            SetStatus("Ready", ACCENT2);
            var sb = GetStartBtn(); var stb = GetStopBtn();
            if (sb != null) { sb.Enabled = true; sb.BackColor = ACCENT; }
            if (stb != null) { stb.Enabled = false; stb.BackColor = BG_SURFACE; stb.ForeColor = TEXT_SEC; }
            if (btnReset != null) btnReset.Enabled = true;
            SetDeviceCardsEnabled(true);
        }

        // ══════════════════════════════════════════════════
        // RESTART DIALOG
        // ══════════════════════════════════════════════════
        private void ShowRestartDialog(string message)
        {
            var dlg = new Form { Text = "Error", Size = new Size(340, 178), StartPosition = FormStartPosition.CenterParent, BackColor = BG_CARD, ForeColor = TEXT_PRI, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            int dm = 1; DwmSetWindowAttribute(dlg.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dm, sizeof(int));
            int cc = ColorTranslator.ToWin32(BG_CARD); DwmSetWindowAttribute(dlg.Handle, DWMWA_CAPTION_COLOR, ref cc, sizeof(int));
            var lbl = new Label { Text = message, ForeColor = DANGER, Font = new Font("Segoe UI", 10F), AutoSize = false, Size = new Size(300, 58), Location = new Point(20, 18), TextAlign = ContentAlignment.MiddleCenter };
            var btn = MakeButton("↺   Restart", ACCENT, BG_DEEP, true);
            btn.Size = new Size(280, 42); btn.Location = new Point(24, 86);
            btn.Click += (s, e) => { dlg.Close(); Application.Restart(); };
            dlg.Controls.Add(lbl); dlg.Controls.Add(btn);
            dlg.ShowDialog(this);
        }

        // ══════════════════════════════════════════════════
        // FORM / TRAY EVENTS
        // ══════════════════════════════════════════════════
        private void Form1_FormClosing_Manual()
        {
            if (!gercektenKapat) { this.Hide(); trayIcon.ShowBalloonTip(2000, "BluetoothTogether", "Running in the background.", ToolTipIcon.Info); }
            else { StopAudioRouting(); if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); } Application.Exit(); }
        }

        private void btnStop_Click(object sender, EventArgs e) => StopAudioRouting();

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!gercektenKapat) { e.Cancel = true; this.Hide(); trayIcon.ShowBalloonTip(2000, "BluetoothTogether", "Running in the background.", ToolTipIcon.Info); }
            else { StopAudioRouting(); if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); } }
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e) { this.Show(); this.WindowState = FormWindowState.Normal; }
        private void OnShowClick(object sender, EventArgs e) { this.Show(); this.WindowState = FormWindowState.Normal; }
        private void OnExitClick(object sender, EventArgs e) { gercektenKapat = true; Application.Exit(); }
    }

    // Graphics extension — FillRoundedRectangle
    internal static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush brush, int x, int y, int w, int h, int r)
        {
            if (w <= 0 || h <= 0) return;
            r = Math.Min(r, Math.Min(w, h) / 2);
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            g.FillPath(brush, path);
        }
    }
}