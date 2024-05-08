namespace AppWinform_main
{
    partial class FormSaveImage
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            ptbVao1 = new PictureBox();
            button1 = new Button();
            button2 = new Button();
            tbTid = new TextBox();
            cbField = new ComboBox();
            ((System.ComponentModel.ISupportInitialize)ptbVao1).BeginInit();
            SuspendLayout();
            // 
            // ptbVao1
            // 
            ptbVao1.BackColor = Color.White;
            ptbVao1.Image = Properties.Resources.CameraIP2;
            ptbVao1.Location = new Point(166, 66);
            ptbVao1.Margin = new Padding(0);
            ptbVao1.Name = "ptbVao1";
            ptbVao1.Size = new Size(468, 318);
            ptbVao1.SizeMode = PictureBoxSizeMode.Zoom;
            ptbVao1.TabIndex = 1;
            ptbVao1.TabStop = false;
            // 
            // button1
            // 
            button1.Location = new Point(12, 12);
            button1.Name = "button1";
            button1.Size = new Size(94, 29);
            button1.TabIndex = 2;
            button1.Text = "Change";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.Location = new Point(12, 66);
            button2.Name = "button2";
            button2.Size = new Size(94, 29);
            button2.TabIndex = 3;
            button2.Text = "Save";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // tbTid
            // 
            tbTid.Location = new Point(166, 13);
            tbTid.Name = "tbTid";
            tbTid.Size = new Size(125, 27);
            tbTid.TabIndex = 4;
            // 
            // cbField
            // 
            cbField.DropDownStyle = ComboBoxStyle.DropDownList;
            cbField.FormattingEnabled = true;
            cbField.Items.AddRange(new object[] { "Xe", "Biển số", "Chủ sở hữu" });
            cbField.Location = new Point(352, 14);
            cbField.Name = "cbField";
            cbField.Size = new Size(151, 28);
            cbField.TabIndex = 5;
            // 
            // FormSaveImage
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(653, 389);
            Controls.Add(cbField);
            Controls.Add(tbTid);
            Controls.Add(button2);
            Controls.Add(button1);
            Controls.Add(ptbVao1);
            Name = "FormSaveImage";
            Text = "FormSaveImage";
            Load += FormSaveImage_Load;
            ((System.ComponentModel.ISupportInitialize)ptbVao1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private PictureBox ptbVao1;
        private Button button1;
        private Button button2;
        private TextBox tbTid;
        private ComboBox cbField;
    }
}