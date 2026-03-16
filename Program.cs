using System;
using System.IO.Pipes;
using System.Threading;
using System.Windows.Forms;

namespace bluetoothTogetheForms
{
    internal static class Program
    {
        private const string PIPE_NAME = "BluetoothTogether_Pipe";
        private const string MUTEX_NAME = "BluetoothTogether_Mutex";

        [STAThread]
        static void Main()
        {
            using var mutex = new Mutex(true, MUTEX_NAME, out bool isNewInstance);

            if (!isNewInstance)
            {
                // Zaten çalýþýyor — pipe üzerinden "göster" komutu gönder
                try
                {
                    using var client = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out);
                    client.Connect(1000); // 1 saniye timeout
                    using var writer = new System.IO.StreamWriter(client);
                    writer.Write("SHOW");
                }
                catch { }
                return;
            }

            ApplicationConfiguration.Initialize();
            var form = new Form1();

            // Arka planda pipe dinle
            var pipeThread = new Thread(() => ListenForShowCommand(form));
            pipeThread.IsBackground = true;
            pipeThread.Start();

            Application.Run(form);
        }

        private static void ListenForShowCommand(Form1 form)
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PIPE_NAME, PipeDirection.In);
                    server.WaitForConnection();
                    using var reader = new System.IO.StreamReader(server);
                    string msg = reader.ReadToEnd();

                    if (msg == "SHOW")
                    {
                        // UI thread'inde çalýþtýr
                        form.Invoke(() =>
                        {
                            form.Show();
                            form.WindowState = FormWindowState.Normal;
                            form.Activate();
                        });
                    }
                }
                catch { }
            }
        }
    }
}