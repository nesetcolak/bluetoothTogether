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

namespace bluetoothTogetheForms
{
    public partial class Form1 : Form
    {
        private WasapiLoopbackCapture capture;
        private List<WasapiOut> outputs = new List<WasapiOut>();
        private List<BufferedWaveProvider> buffers = new List<BufferedWaveProvider>();
        private List<MMDevice> availableDevices = new List<MMDevice>();

        // Ýki kütüphaneyi de en baþta TEK SEFERLÝK tanýmlýyoruz
        private MMDeviceEnumerator enumerator;
        private CoreAudioController audioController;

        private MMDevice virtualCableDevice;
        private string originalDeviceId = "";

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private bool gercektenKapat = false;


        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_MICA_EFFECT = 1029;

        // EKLENEN YENÝ ÞÝFRE: Baþlýk çubuðunun rengini zorla deðiþtirme (Win 11)
        private const int DWMWA_CAPTION_COLOR = 35;
        public Form1()
        {
            InitializeComponent();
            // 1. Windows 11/10 Karanlýk Baþlýk Çubuðu (Genel Sistem Ýsteði)
            int darkMode = 1;
            DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // 2. KESÝN ÇÖZÜM: Baþlýk çubuðunun rengini formun arka planýyla ayný (28, 28, 28) yap!
            // ColorTranslator ile RGB rengimizi Windows'un anladýðý Win32 formatýna çeviriyoruz
            int captionColor = ColorTranslator.ToWin32(Color.FromArgb(28, 28, 28));
            DwmSetWindowAttribute(this.Handle, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

            // 3. Mica Efekti
            int mica = 1;
            DwmSetWindowAttribute(this.Handle, DWMWA_MICA_EFFECT, ref mica, sizeof(int));

            // 4. Modern Temayý Uygula
            ApplyModernTheme();
        }

        private void ApplyModernTheme()
        {
            this.BackColor = Color.FromArgb(28, 28, 28); // Görseldeki o koyu gri ton
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            this.Text = "BluetoothTogether";

            // Durum Yazýsý (lblStatus) Ayarlarý
            lblStatus.AutoSize = false; // Manuel boyutlandýrma için kapattýk
            lblStatus.Width = clbDevices.Width; // Geniþliði liste kutusuyla ayný yaptýk
            lblStatus.TextAlign = ContentAlignment.MiddleCenter; // Yazýyý kutu içinde ortaladýk
            lblStatus.ForeColor = Color.FromArgb(255, 165, 0); // O turuncu/sarý ton

            // Cihaz Listesi (CheckedListBox) Tasarýmý
            clbDevices.BackColor = Color.FromArgb(45, 45, 45); // Arka planý koyulaþtýrdýk
            clbDevices.ForeColor = Color.White; // Yazýlar beyaz
            clbDevices.BorderStyle = BorderStyle.None; // Kenarlýklarý kaldýrdýk
            clbDevices.CheckOnClick = true;

            // Buton Tasarýmlarý
            StyleButton(btnStart, Color.White, Color.Black, "START");
            StyleButton(btnStop, Color.White, Color.Black, "STOP");

            // --- SAÐ ALT KÖÞE (GÝZLÝ SÝMGELER) AYARLARI ---
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Göster", null, OnShowClick);
            trayMenu.Items.Add("Çýkýþ", null, OnExitClick);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "BluetoothTogether";
            // Formun sol üstüne koyduðun ikonu sað alta da aynen kopyalar
            trayIcon.Icon = this.Icon;
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += TrayIcon_DoubleClick;
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

            // Kenarlarý hafif yumuþatmak için (Opsiyonel: Region ayarý eklenebilir)
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 200, 200);
        }


        // DÝKKAT: Uygulama açýlýþýný async yaptýk ki donmasýn
        private async void Form1_Load(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            btnStop.Enabled = false;
            clbDevices.Enabled = false;
            lblStatus.Text = "Durum: Sistem Hazýrlanýyor, Bekleyin...";
            lblStatus.ForeColor = Color.Orange;

            // O hantal 5 saniyelik yüklemeyi ARKA PLANDA SADECE BÝR KERE yapýyoruz!
            await Task.Run(() =>
            {
                enumerator = new MMDeviceEnumerator();
                audioController = new CoreAudioController();
            });

            // Yükleme bitti, cihazlarý tarayalým
            var allDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

            virtualCableDevice = allDevices.FirstOrDefault(d =>
                d.FriendlyName.IndexOf("bluetoothTogether", StringComparison.OrdinalIgnoreCase) >= 0 ||
                d.FriendlyName.IndexOf("VB-Audio", StringComparison.OrdinalIgnoreCase) >= 0);

            if (virtualCableDevice == null)
            {
                MessageBox.Show("Sanal Kablo bulunamadý! Lütfen kurulu olduðundan emin olun.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Durum: Hata (Sanal Kablo Yok)";
                lblStatus.ForeColor = Color.Red;
                return;
            }

            availableDevices = allDevices.Where(d => d.ID != virtualCableDevice.ID).ToList();
            clbDevices.Items.Clear();

            for (int i = 0; i < availableDevices.Count; i++)
            {
                var dev = availableDevices[i];
                string name = dev.FriendlyName;
                clbDevices.Items.Add(name);

                if (name.IndexOf("Kulaklýk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Headphone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Bluetooth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Stereo", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    clbDevices.SetItemChecked(i, true);
                }
            }

            // Arayüzü kullanýma aç
            btnStart.Enabled = true;
            clbDevices.Enabled = true;
            lblStatus.Text = "Durum: Bekleniyor...";
            lblStatus.ForeColor = Color.White;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (clbDevices.CheckedIndices.Count == 0)
            {
                MessageBox.Show("Lütfen en az bir cihaz seçin!", "Uyarý", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // 1. MEVCUT CÝHAZIN ID'SÝNÝ KAYDET
                var currentDefault = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (currentDefault != null && currentDefault.ID != virtualCableDevice.ID)
                {
                    originalDeviceId = currentDefault.ID;
                }

                // 2. HAFIZADAKÝ KÜTÜPHANE ÝLE ANINDA GEÇÝÞ YAP (Gecikme Yok!)
                var cableToSwitch = audioController.GetDevices().FirstOrDefault(d => d.RealId == virtualCableDevice.ID);
                if (cableToSwitch != null)
                {
                    cableToSwitch.SetAsDefault();
                    cableToSwitch.SetAsDefaultCommunications();
                }

                // NAudio'nun çökmemesi için sadece 200 milisaniye pay
                System.Threading.Thread.Sleep(200);

                // 3. SES DAÐITIMINI BAÞLAT
                capture = new WasapiLoopbackCapture(virtualCableDevice);

                foreach (int index in clbDevices.CheckedIndices)
                {
                    var device = availableDevices[index];
                    var buffer = new BufferedWaveProvider(capture.WaveFormat) { DiscardOnBufferOverflow = true };
                    var output = new WasapiOut(device, AudioClientShareMode.Shared, true, 50);

                    output.Init(buffer);
                    output.Play();

                    buffers.Add(buffer);
                    outputs.Add(output);
                }

                capture.DataAvailable += (s, args) =>
                {
                    foreach (var buffer in buffers)
                    {
                        buffer.AddSamples(args.Buffer, 0, args.BytesRecorded);
                    }
                };

                capture.StartRecording();

                lblStatus.Text = $"Durum: {clbDevices.CheckedIndices.Count} Cihaza Yönlendiriliyor";
                lblStatus.ForeColor = Color.Green;

                btnStart.Enabled = false;
                clbDevices.Enabled = false;
                btnStop.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata oluþtu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                StopAudioRouting();
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopAudioRouting();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!gercektenKapat)
            {
                // Çarpýya basýldýysa kapanmayý iptal et ve formu gizle
                e.Cancel = true;
                this.Hide();

                // Kullanýcýya sað alttan minik bir bildirim ver (Sadece ilk seferde çalýþýr)
                trayIcon.ShowBalloonTip(2000, "BluetoothTogether", "Uygulama arka planda çalýþmaya devam ediyor.", ToolTipIcon.Info);
            }
            else
            {
                // Menüden "Çýkýþ"a basýldýysa sesi durdur ve gerçekten kapat
                StopAudioRouting();

                // Hafýzadaki sað alt ikonunu yok et (yoksa bugda kalýr)
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }
            }
        }

        private void StopAudioRouting()
        {
            if (capture != null)
            {
                capture.StopRecording();
                capture.Dispose();
                capture = null;
            }

            foreach (var output in outputs)
            {
                if (output != null)
                {
                    output.Stop();
                    output.Dispose();
                }
            }

            outputs.Clear();
            buffers.Clear();

            // 4. ÝÞLEM BÝTÝNCE ESKÝ CÝHAZA ANINDA GERÝ DÖN (Hafýzadaki kütüphane ile)
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
        }
        // Sað alttaki ikona çift týklanýnca formu geri getir
        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        // Sað týk menüsünden "Göster"e basýlýnca formu geri getir
        private void OnShowClick(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        // Sað týk menüsünden "Çýkýþ"a basýlýnca uygulamayý komple kapat
        private void OnExitClick(object sender, EventArgs e)
        {
            gercektenKapat = true; // Artýk formun kapanmasýna izin veriyoruz
            Application.Exit(); // Kapatma emrini ver
        }
    }

}