using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using System.Runtime.InteropServices;
using System.Diagnostics;

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

        // Ses seviyesi senkronizasyonu için
        private System.Windows.Forms.Timer volumeSyncTimer;
        private float lastVolume = -1f;
        private bool lastMute = false;

        // Yeni buton
        private Button btnReset;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_MICA_EFFECT = 1029;
        private const int DWMWA_CAPTION_COLOR = 35;

        public Form1()
        {
            InitializeComponent();

            int darkMode = 1;
            DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            int captionColor = ColorTranslator.ToWin32(Color.FromArgb(28, 28, 28));
            DwmSetWindowAttribute(this.Handle, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

            int mica = 1;
            DwmSetWindowAttribute(this.Handle, DWMWA_MICA_EFFECT, ref mica, sizeof(int));

            ApplyModernTheme();
        }

        private void ApplyModernTheme()
        {
            this.BackColor = Color.FromArgb(28, 28, 28);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            this.Text = "BluetoothTogether";

            lblStatus.AutoSize = false;
            lblStatus.Width = clbDevices.Width;
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            lblStatus.ForeColor = Color.FromArgb(255, 165, 0);

            clbDevices.BackColor = Color.FromArgb(45, 45, 45);
            clbDevices.ForeColor = Color.White;
            clbDevices.BorderStyle = BorderStyle.None;
            clbDevices.CheckOnClick = true;

            StyleButton(btnStart, Color.White, Color.Black, "START");
            StyleButton(btnStop, Color.White, Color.Black, "STOP");

            // Reset butonunu dinamik olarak ekle (Designer'da yoksa)
            btnReset = new Button();
            btnReset.Size = new Size(80, btnStart.Height);
            // btnStart'ın sağına koy — konumu formdaki düzene göre ayarla
            btnReset.Location = new Point(btnStop.Right + 10, btnStop.Top);
            btnReset.Anchor = btnStop.Anchor;
            StyleButton(btnReset, Color.FromArgb(60, 60, 60), Color.White, "↺ RESET");
            btnReset.FlatAppearance.BorderSize = 1;
            btnReset.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            btnReset.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            btnReset.Click += BtnReset_Click;
            this.Controls.Add(btnReset);

            // Tray
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Göster", null, OnShowClick);
            trayMenu.Items.Add("Çıkış", null, OnExitClick);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "BluetoothTogether";
            trayIcon.Icon = this.Icon;
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += TrayIcon_DoubleClick;

            // Ses senkronizasyon timer'ı - 250ms yeterli, daha sık yazmak audio pipeline'ını yoruyor
            volumeSyncTimer = new System.Windows.Forms.Timer();
            volumeSyncTimer.Interval = 250;
            volumeSyncTimer.Tick += VolumeSyncTimer_Tick;
        }

        private void StyleButton(Button btn, Color backColor, Color textColor, string text)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = backColor;
            btn.ForeColor = textColor;
            btn.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.Text = text;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 200, 200);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            // === VB-AUDIO KURULUM KONTROLÜ ===
            if (!IsVirtualCableInstalled())
            {
                ShowVirtualCableInstallPrompt();
                return;
            }

            await InitializeDevicesAsync();
        }

        /// <summary>
        /// VB-Audio Virtual Cable'ın sistemde kurulu olup olmadığını kontrol eder.
        /// </summary>
        private bool IsVirtualCableInstalled()
        {
            try
            {
                var tempEnumerator = new MMDeviceEnumerator();
                var allDevices = tempEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
                return allDevices.Any(d =>
                    d.FriendlyName.IndexOf("VB-Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.FriendlyName.IndexOf("CABLE Input", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.FriendlyName.IndexOf("bluetoothTogether", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// VB-Audio kurulu değilse kullanıcıya açıklayıcı bir ekran gösterir.
        /// </summary>
        private void ShowVirtualCableInstallPrompt()
        {
            // Mevcut kontrolleri gizle
            btnStart.Visible = false;
            btnStop.Visible = false;
            if (btnReset != null) btnReset.Visible = false;
            clbDevices.Visible = false;
            lblStatus.Visible = false;

            // Bilgi paneli oluştur
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(20)
            };

            var lblTitle = new Label
            {
                Text = "⚠ Sanal Ses Sürücüsü Gerekli",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 165, 0),
                AutoSize = false,
                Width = 400,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 30)
            };

            var lblInfo = new Label
            {
                Text = "BluetoothTogether'ın çalışabilmesi için sisteminizde\n" +
                       "\"VB-Audio Virtual Cable\" kurulu olması gerekiyor.\n\n" +
                       "Tamamen ücretsizdir, kurulumu 1 dakika alır.\n" +
                       "Kurduktan sonra uygulamayı yeniden başlatın.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(200, 200, 200),
                AutoSize = false,
                Width = 400,
                Height = 100,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 75)
            };

            var btnDownload = new Button
            {
                Text = "⬇  VB-Audio'yu İndir (vb-audio.com)",
                Size = new Size(320, 42),
                Location = new Point(60, 190),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            StyleButton(btnDownload, Color.FromArgb(255, 165, 0), Color.Black, "⬇  VB-Audio'yu İndir (vb-audio.com)");
            btnDownload.Click += (s, e) =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://vb-audio.com/Cable/",
                    UseShellExecute = true
                });
            };

            var btnRecheck = new Button
            {
                Text = "✓  Kurdum, Tekrar Kontrol Et",
                Size = new Size(320, 42),
                Location = new Point(60, 245),
                Cursor = Cursors.Hand
            };
            StyleButton(btnRecheck, Color.FromArgb(60, 60, 60), Color.White, "✓  Kurdum, Tekrar Kontrol Et");
            btnRecheck.FlatAppearance.BorderSize = 1;
            btnRecheck.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            btnRecheck.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            btnRecheck.Click += async (s, e) =>
            {
                if (IsVirtualCableInstalled())
                {
                    // Kurulmuş! Paneli kaldır ve normal akışa devam et
                    panel.Visible = false;
                    btnStart.Visible = true;
                    btnStop.Visible = true;
                    if (btnReset != null) btnReset.Visible = true;
                    clbDevices.Visible = true;
                    lblStatus.Visible = true;
                    await InitializeDevicesAsync();
                }
                else
                {
                    MessageBox.Show(
                        "VB-Audio hâlâ bulunamadı.\n\nLütfen kurulumu tamamlayıp bilgisayarınızı yeniden başlatın.",
                        "Bulunamadı",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            };

            panel.Controls.Add(lblTitle);
            panel.Controls.Add(lblInfo);
            panel.Controls.Add(btnDownload);
            panel.Controls.Add(btnRecheck);
            this.Controls.Add(panel);
            panel.BringToFront();
        }

        private async Task InitializeDevicesAsync()
        {
            btnStart.Enabled = false;
            btnStop.Enabled = false;
            if (btnReset != null) btnReset.Enabled = false;
            clbDevices.Enabled = false;
            lblStatus.Text = "Durum: Sistem Hazırlanıyor, Bekleyin...";
            lblStatus.ForeColor = Color.Orange;

            await Task.Run(() =>
            {
                enumerator = new MMDeviceEnumerator();
                audioController = new CoreAudioController();
            });

            ScanDevices();

            if (virtualCableDevice == null)
            {
                lblStatus.Text = "Durum: Hata (Sanal Kablo Yok)";
                lblStatus.ForeColor = Color.Red;
                ShowRestartDialog("Beklenmeyen bir hata oluştu.\nYeniden başlatın.");
                return;
            }

            btnStart.Enabled = true;
            if (btnReset != null) btnReset.Enabled = true;
            clbDevices.Enabled = true;
            lblStatus.Text = "Durum: Bekleniyor...";
            lblStatus.ForeColor = Color.White;
        }

        /// <summary>
        /// Cihaz listesini tarar ve UI'ı günceller. Reset butonundan da çağrılır.
        /// </summary>
        private void ScanDevices()
        {
            var allDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

            virtualCableDevice = allDevices.FirstOrDefault(d =>
                d.FriendlyName.IndexOf("bluetoothTogether", StringComparison.OrdinalIgnoreCase) >= 0 ||
                d.FriendlyName.IndexOf("VB-Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                d.FriendlyName.IndexOf("CABLE Input", StringComparison.OrdinalIgnoreCase) >= 0);

            availableDevices = allDevices.Where(d => d.ID != virtualCableDevice?.ID).ToList();
            clbDevices.Items.Clear();

            for (int i = 0; i < availableDevices.Count; i++)
            {
                var dev = availableDevices[i];
                string name = dev.FriendlyName;
                clbDevices.Items.Add(name);

                if (name.IndexOf("Kulaklık", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Headphone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Bluetooth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Stereo", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    clbDevices.SetItemChecked(i, true);
                }
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            bool wasRunning = (capture != null);

            if (wasRunning)
                StopAudioRouting();

            lblStatus.Text = "Durum: Cihazlar Taranıyor...";
            lblStatus.ForeColor = Color.Orange;

            ScanDevices();

            lblStatus.Text = $"Durum: {availableDevices.Count} cihaz bulundu.";
            lblStatus.ForeColor = Color.White;

            if (wasRunning)
                MessageBox.Show("Ses yönlendirme durduruldu. Cihazlar yenilendi.\nSTART ile tekrar başlatabilirsiniz.", "Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (clbDevices.CheckedIndices.Count == 0)
            {
                MessageBox.Show("Lütfen en az bir cihaz seçin!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var currentDefault = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                if (virtualCableDevice == null) 
                {
                    ShowRestartDialog("Beklenmeyen bir hata oluştu.\nYeniden başlatın.");
                    
                    
                }
                
                if (currentDefault != null && currentDefault.ID != virtualCableDevice.ID)
                    originalDeviceId = currentDefault.ID;

                var cableToSwitch = audioController.GetDevices().FirstOrDefault(d => d.RealId == virtualCableDevice.ID);
                if (cableToSwitch != null)
                {
                    cableToSwitch.SetAsDefault();
                    cableToSwitch.SetAsDefaultCommunications();
                }

                capture = new WasapiLoopbackCapture(virtualCableDevice);

                foreach (int index in clbDevices.CheckedIndices)
                {
                    var device = availableDevices[index];
                    var buffer = new BufferedWaveProvider(capture.WaveFormat) { DiscardOnBufferOverflow = true };
                    var output = new WasapiOut(device, AudioClientShareMode.Shared, true, 20);

                    output.Init(buffer);
                    output.Play();

                    buffers.Add(buffer);
                    outputs.Add(output);
                }

                capture.DataAvailable += (s, args) =>
                {
                    foreach (var buffer in buffers)
                        buffer.AddSamples(args.Buffer, 0, args.BytesRecorded);
                };

                capture.StartRecording();

                // Ses senkronizasyonunu başlat
                lastVolume = -1f; // İlk tick'te mutlaka senkronize etsin
                lastMute = false;
                volumeSyncTimer.Start();

                lblStatus.Text = $"Durum: {clbDevices.CheckedIndices.Count} Cihaza Yönlendiriliyor";
                lblStatus.ForeColor = Color.Green;

                btnStart.Enabled = false;
                clbDevices.Enabled = false;
                if (btnReset != null) btnReset.Enabled = false;
                btnStop.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                StopAudioRouting();
            }
        }

        /// <summary>
        /// Her 100ms'de bir Windows ana ses seviyesini okur,
        /// değişmişse tüm çıkış cihazlarına uygular.
        /// </summary>
        private void VolumeSyncTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Virtual Cable'ın ses seviyesini değil, orijinal cihazı dinliyoruz —
                // ama asıl kaynak VB-Cable olduğundan sistemin master volume'ünü izliyoruz.
                // En güvenilir yol: VB-Cable cihazının kendi volume'ünü oku.
                if (virtualCableDevice == null) return;

                float currentVolume = virtualCableDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
                bool currentMute = virtualCableDevice.AudioEndpointVolume.Mute;

                // Değişiklik yoksa işlem yapma
                if (Math.Abs(currentVolume - lastVolume) < 0.001f && currentMute == lastMute)
                    return;

                lastVolume = currentVolume;
                lastMute = currentMute;

                // Tüm aktif çıkış cihazlarına aynı ses seviyesini uygula
                foreach (int index in clbDevices.CheckedIndices)
                {
                    try
                    {
                        var device = availableDevices[index];
                        device.AudioEndpointVolume.MasterVolumeLevelScalar = currentVolume;
                        device.AudioEndpointVolume.Mute = currentMute;
                    }
                    catch { /* Bir cihaz hata verse bile diğerleri devam etsin */ }
                }
            }
            catch { /* Timer tick'i çökmemeli */ }
        }

        private void ShowRestartDialog(string message)
        {
            var dialog = new Form
            {
                Text = "HATA",
                Size = new Size(340, 180),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(28, 28, 28),
                ForeColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            // Başlık çubuğunu da karanlık yap
            int darkMode = 1;
            DwmSetWindowAttribute(dialog.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            int captionColor = ColorTranslator.ToWin32(Color.FromArgb(28, 28, 28));
            DwmSetWindowAttribute(dialog.Handle, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

            var lbl = new Label
            {
                Text = message,
                ForeColor = Color.FromArgb(255, 80, 80),
                Font = new Font("Segoe UI", 10F),
                AutoSize = false,
                Size = new Size(300, 60),
                Location = new Point(20, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var btnRestart = new Button
            {
                Text = "↺  Yeniden Başlat",
                Size = new Size(280, 40),
                Location = new Point(20, 90),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnRestart.FlatAppearance.BorderSize = 0;
            btnRestart.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 200, 200);
            btnRestart.Click += (s, e) =>
            {
                dialog.Close();
                // Mevcut exe'yi yeniden başlat
                Application.Restart();
                
            };

            dialog.Controls.Add(lbl);
            dialog.Controls.Add(btnRestart);
            dialog.ShowDialog(this);
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopAudioRouting();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!gercektenKapat)
            {
                e.Cancel = true;
                this.Hide();
                trayIcon.ShowBalloonTip(2000, "BluetoothTogether", "Uygulama arka planda çalışmaya devam ediyor.", ToolTipIcon.Info);
            }
            else
            {
                StopAudioRouting();
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }
            }
        }

        private void StopAudioRouting()
        {
            volumeSyncTimer?.Stop();

            if (capture != null)
            {
                capture.StopRecording();
                capture.Dispose();
                capture = null;
            }

            foreach (var output in outputs)
            {
                try { output?.Stop(); output?.Dispose(); } catch { }
            }

            outputs.Clear();
            buffers.Clear();

            if (!string.IsNullOrEmpty(originalDeviceId))
            {
                var originalDeviceToSwitch = audioController.GetDevices().FirstOrDefault(d => d.RealId == originalDeviceId);
                if (originalDeviceToSwitch != null)
                {
                    originalDeviceToSwitch.SetAsDefault();
                    originalDeviceToSwitch.SetAsDefaultCommunications();
                }
                originalDeviceId = "";
            }

            lblStatus.Text = "Durum: Bekleniyor...";
            lblStatus.ForeColor = Color.White;

            if (btnStart != null) btnStart.Enabled = true;
            if (clbDevices != null) clbDevices.Enabled = true;
            if (btnStop != null) btnStop.Enabled = false;
            if (btnReset != null) btnReset.Enabled = true;
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void OnShowClick(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            gercektenKapat = true;
            Application.Exit();
        }
    }
}