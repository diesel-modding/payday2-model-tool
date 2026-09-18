namespace PD2ModelParser.UI
{
    partial class ObjectsPanel
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
            System.Windows.Forms.Label lblModel;
            System.Windows.Forms.Label lblScript;
            showScriptChanges = new System.Windows.Forms.CheckBox();
            btnReload = new System.Windows.Forms.Button();
            treeView = new System.Windows.Forms.TreeView();
            propertyGrid1 = new System.Windows.Forms.PropertyGrid();
            scriptFile = new FileBrowserControl();
            modelFile = new FileBrowserControl();
            splitContainer1 = new System.Windows.Forms.SplitContainer();
            btnSave = new System.Windows.Forms.Button();
            lblModel = new System.Windows.Forms.Label();
            lblScript = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // lblModel
            // 
            lblModel.AutoSize = true;
            lblModel.Location = new System.Drawing.Point(27, 9);
            lblModel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblModel.Name = "lblModel";
            lblModel.Size = new System.Drawing.Size(75, 15);
            lblModel.TabIndex = 2;
            lblModel.Text = "Select Model";
            // 
            // lblScript
            // 
            lblScript.AutoSize = true;
            lblScript.Location = new System.Drawing.Point(29, 43);
            lblScript.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblScript.Name = "lblScript";
            lblScript.Size = new System.Drawing.Size(71, 15);
            lblScript.TabIndex = 3;
            lblScript.Text = "Select Script";
            // 
            // showScriptChanges
            // 
            showScriptChanges.AutoSize = true;
            showScriptChanges.Checked = true;
            showScriptChanges.CheckState = System.Windows.Forms.CheckState.Checked;
            showScriptChanges.Location = new System.Drawing.Point(114, 70);
            showScriptChanges.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            showScriptChanges.Name = "showScriptChanges";
            showScriptChanges.Size = new System.Drawing.Size(137, 19);
            showScriptChanges.TabIndex = 4;
            showScriptChanges.Text = "Show Script Changes";
            showScriptChanges.UseVisualStyleBackColor = true;
            showScriptChanges.CheckedChanged += ShowScriptChanges_CheckedChanged;
            // 
            // btnReload
            // 
            btnReload.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnReload.Location = new System.Drawing.Point(579, 70);
            btnReload.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnReload.Name = "btnReload";
            btnReload.Size = new System.Drawing.Size(126, 27);
            btnReload.TabIndex = 5;
            btnReload.Text = "Reload";
            btnReload.UseVisualStyleBackColor = true;
            btnReload.Click += BtnReload_Click;
            // 
            // treeView
            // 
            treeView.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            treeView.Location = new System.Drawing.Point(0, 0);
            treeView.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            treeView.Name = "treeView";
            treeView.Size = new System.Drawing.Size(437, 283);
            treeView.TabIndex = 6;
            treeView.NodeMouseClick += TreeView_NodeMouseClick;
            // 
            // propertyGrid1
            // 
            propertyGrid1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            propertyGrid1.BackColor = System.Drawing.SystemColors.Control;
            propertyGrid1.Location = new System.Drawing.Point(0, 0);
            propertyGrid1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            propertyGrid1.Name = "propertyGrid1";
            propertyGrid1.Size = new System.Drawing.Size(259, 283);
            propertyGrid1.TabIndex = 7;
            // 
            // scriptFile
            // 
            scriptFile.AllowDrop = true;
            scriptFile.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            scriptFile.Location = new System.Drawing.Point(114, 37);
            scriptFile.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            scriptFile.Name = "scriptFile";
            scriptFile.Size = new System.Drawing.Size(590, 27);
            scriptFile.TabIndex = 1;
            scriptFile.FileSelected += FileBrowserControl2_FileSelected;
            // 
            // modelFile
            // 
            modelFile.AllowDrop = true;
            modelFile.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            modelFile.Location = new System.Drawing.Point(114, 3);
            modelFile.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            modelFile.Name = "modelFile";
            modelFile.Size = new System.Drawing.Size(590, 27);
            modelFile.TabIndex = 0;
            modelFile.FileSelected += FileBrowserControl1_FileSelected;
            // 
            // splitContainer1
            // 
            splitContainer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            splitContainer1.Location = new System.Drawing.Point(4, 104);
            splitContainer1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(treeView);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(propertyGrid1);
            splitContainer1.Size = new System.Drawing.Size(701, 283);
            splitContainer1.SplitterDistance = 437;
            splitContainer1.SplitterWidth = 5;
            splitContainer1.TabIndex = 8;
            // 
            // btnSave
            // 
            btnSave.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnSave.Location = new System.Drawing.Point(461, 70);
            btnSave.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnSave.Name = "btnSave";
            btnSave.Size = new System.Drawing.Size(111, 27);
            btnSave.TabIndex = 9;
            btnSave.Text = "Save (in place)";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += BtnSave_Click;
            // 
            // ObjectsPanel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(btnSave);
            Controls.Add(splitContainer1);
            Controls.Add(btnReload);
            Controls.Add(showScriptChanges);
            Controls.Add(lblScript);
            Controls.Add(lblModel);
            Controls.Add(scriptFile);
            Controls.Add(modelFile);
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Name = "ObjectsPanel";
            Size = new System.Drawing.Size(708, 390);
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private FileBrowserControl modelFile;
        private FileBrowserControl scriptFile;
        private System.Windows.Forms.CheckBox showScriptChanges;
        private System.Windows.Forms.Button btnReload;
        private System.Windows.Forms.TreeView treeView;
        private System.Windows.Forms.PropertyGrid propertyGrid1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button btnSave;
    }
}
