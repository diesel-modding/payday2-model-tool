namespace PD2ModelParser.UI
{
    partial class ExportPanel
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            exportBttn = new System.Windows.Forms.Button();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            formatBox = new System.Windows.Forms.ComboBox();
            inputFileBox = new FileBrowserControl();
            SuspendLayout();
            // 
            // exportBttn
            // 
            exportBttn.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            exportBttn.BackColor = System.Drawing.SystemColors.ControlLightLight;
            exportBttn.Enabled = false;
            exportBttn.Location = new System.Drawing.Point(6, 350);
            exportBttn.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            exportBttn.Name = "exportBttn";
            exportBttn.Size = new System.Drawing.Size(698, 27);
            exportBttn.TabIndex = 17;
            exportBttn.Text = "Convert";
            exportBttn.UseVisualStyleBackColor = false;
            exportBttn.Click += ExportBttn_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(4, 14);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(59, 15);
            label1.TabIndex = 14;
            label1.Text = "Input File:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(6, 45);
            label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(48, 15);
            label2.TabIndex = 18;
            label2.Text = "Format:";
            // 
            // formatBox
            // 
            formatBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            formatBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            formatBox.FormattingEnabled = true;
            formatBox.Items.AddRange(new object[] { "If you can see this at runtime, something strange has happened." });
            formatBox.Location = new System.Drawing.Point(82, 42);
            formatBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            formatBox.Name = "formatBox";
            formatBox.Size = new System.Drawing.Size(622, 23);
            formatBox.TabIndex = 19;
            // 
            // inputFileBox
            // 
            inputFileBox.AllowDrop = true;
            inputFileBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            inputFileBox.Location = new System.Drawing.Point(82, 8);
            inputFileBox.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            inputFileBox.Name = "inputFileBox";
            inputFileBox.Size = new System.Drawing.Size(623, 27);
            inputFileBox.TabIndex = 20;
            inputFileBox.FileSelected += InputFileBox_FileSelected;
            // 
            // ExportPanel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(inputFileBox);
            Controls.Add(label2);
            Controls.Add(formatBox);
            Controls.Add(label1);
            Controls.Add(exportBttn);
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Name = "ExportPanel";
            Size = new System.Drawing.Size(708, 390);
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button exportBttn;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox formatBox;
        private FileBrowserControl inputFileBox;
    }
}
