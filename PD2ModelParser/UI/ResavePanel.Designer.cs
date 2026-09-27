namespace PD2ModelParser.UI
{
    partial class ResavePanel
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ResavePanel));
            inputFileBox = new FileBrowserControl();
            label1 = new System.Windows.Forms.Label();
            exportBttn = new System.Windows.Forms.Button();
            richTextBox = new System.Windows.Forms.RichTextBox();
            SuspendLayout();
            // 
            // inputFileBox
            // 
            inputFileBox.AllowDrop = true;
            inputFileBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            inputFileBox.Location = new System.Drawing.Point(82, 8);
            inputFileBox.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            inputFileBox.Name = "inputFileBox";
            inputFileBox.Size = new System.Drawing.Size(623, 27);
            inputFileBox.TabIndex = 25;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(4, 14);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(59, 15);
            label1.TabIndex = 21;
            label1.Text = "Input File:";
            // 
            // exportBttn
            // 
            exportBttn.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            exportBttn.BackColor = System.Drawing.SystemColors.ControlLightLight;
            exportBttn.Enabled = false;
            exportBttn.Location = new System.Drawing.Point(6, 41);
            exportBttn.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            exportBttn.Name = "exportBttn";
            exportBttn.Size = new System.Drawing.Size(698, 27);
            exportBttn.TabIndex = 22;
            exportBttn.Text = "Convert";
            exportBttn.UseVisualStyleBackColor = false;
            exportBttn.Click += ExportBttn_Click;
            // 
            // richTextBox
            // 
            richTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            richTextBox.BackColor = System.Drawing.SystemColors.ControlLight;
            richTextBox.Location = new System.Drawing.Point(5, 246);
            richTextBox.Margin = new System.Windows.Forms.Padding(5);
            richTextBox.Name = "richTextBox";
            richTextBox.ReadOnly = true;
            richTextBox.Size = new System.Drawing.Size(698, 139);
            richTextBox.TabIndex = 26;
            richTextBox.Text = resources.GetString("richTextBox.Text");
            // 
            // ResavePanel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(richTextBox);
            Controls.Add(inputFileBox);
            Controls.Add(label1);
            Controls.Add(exportBttn);
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Name = "ResavePanel";
            Size = new System.Drawing.Size(708, 390);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private FileBrowserControl inputFileBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button exportBttn;
        private System.Windows.Forms.RichTextBox richTextBox;
    }
}
