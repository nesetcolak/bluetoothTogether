namespace bluetoothTogetheForms
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            lblStatus = new Label();
            clbDevices = new CheckedListBox();
            btnStop = new Button();
            btnStart = new Button();
            SuspendLayout();
            // 
            // lblStatus
            // 
            lblStatus.Location = new Point(139, 38);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(277, 30);
            lblStatus.TabIndex = 13;
            lblStatus.Text = "label1";
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // clbDevices
            // 
            clbDevices.BackColor = SystemColors.Control;
            clbDevices.FormattingEnabled = true;
            clbDevices.Location = new Point(139, 71);
            clbDevices.Name = "clbDevices";
            clbDevices.Size = new Size(277, 94);
            clbDevices.TabIndex = 12;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(278, 183);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(95, 33);
            btnStop.TabIndex = 11;
            btnStop.Text = "STOP";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(177, 183);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(95, 33);
            btnStart.TabIndex = 10;
            btnStart.Text = "START";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(587, 285);
            Controls.Add(lblStatus);
            Controls.Add(clbDevices);
            Controls.Add(btnStop);
            Controls.Add(btnStart);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "Form1";
            Text = "BluetoothTogether";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            ResumeLayout(false);
        }

        #endregion

        private Label lblStatus;
        private CheckedListBox clbDevices;
        private Button btnStop;
        private Button btnStart;
    }
}
