using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace PD2ModelParser
{
    partial class Form1
    {

        private FolderBrowserDialog folderBrowserDialog;

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
            ComponentResourceManager resources = new ComponentResourceManager(typeof(Form1));
            folderBrowserDialog = new FolderBrowserDialog();
            mainTabs = new TabControl();
            importTab = new TabPage();
            importPanel = new PD2ModelParser.UI.ImportPanel();
            exportTab = new TabPage();
            exportPanel = new PD2ModelParser.UI.ExportPanel();
            resaveTab = new TabPage();
            resavePanel = new PD2ModelParser.UI.ResavePanel();
            objectsTab = new TabPage();
            objectsPanel = new PD2ModelParser.UI.ObjectsPanel();
            hashTab = new TabPage();
            hashPanel = new PD2ModelParser.UI.HashPanel();
            helpTab = new TabPage();
            helpPanel = new PD2ModelParser.UI.HelpPanel();
            mainTabs.SuspendLayout();
            importTab.SuspendLayout();
            exportTab.SuspendLayout();
            resaveTab.SuspendLayout();
            objectsTab.SuspendLayout();
            hashTab.SuspendLayout();
            helpTab.SuspendLayout();
            SuspendLayout();
            // 
            // mainTabs
            // 
            mainTabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            mainTabs.Controls.Add(importTab);
            mainTabs.Controls.Add(exportTab);
            mainTabs.Controls.Add(resaveTab);
            mainTabs.Controls.Add(objectsTab);
            mainTabs.Controls.Add(hashTab);
            mainTabs.Controls.Add(helpTab);
            mainTabs.Location = new Point(14, 14);
            mainTabs.Margin = new Padding(4, 3, 4, 3);
            mainTabs.Name = "mainTabs";
            mainTabs.SelectedIndex = 0;
            mainTabs.Size = new Size(798, 405);
            mainTabs.TabIndex = 14;
            // 
            // importTab
            // 
            importTab.BackColor = SystemColors.ControlLight;
            importTab.Controls.Add(importPanel);
            importTab.Location = new Point(4, 24);
            importTab.Margin = new Padding(4, 3, 4, 3);
            importTab.Name = "importTab";
            importTab.Padding = new Padding(4, 3, 4, 3);
            importTab.Size = new Size(790, 377);
            importTab.TabIndex = 0;
            importTab.Text = "Import";
            // 
            // importPanel
            // 
            importPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            importPanel.BackColor = SystemColors.ControlLight;
            importPanel.Font = new Font("Segoe UI", 9F);
            importPanel.ForeColor = SystemColors.ControlText;
            importPanel.Location = new Point(3, 3);
            importPanel.Margin = new Padding(0);
            importPanel.MaximumSize = new Size(786, 371);
            importPanel.MinimumSize = new Size(786, 350);
            importPanel.Name = "importPanel";
            importPanel.Size = new Size(786, 371);
            importPanel.TabIndex = 0;
            // 
            // exportTab
            // 
            exportTab.BackColor = SystemColors.ControlLight;
            exportTab.Controls.Add(exportPanel);
            exportTab.Location = new Point(4, 24);
            exportTab.Margin = new Padding(4, 3, 4, 3);
            exportTab.Name = "exportTab";
            exportTab.Padding = new Padding(4, 3, 4, 3);
            exportTab.Size = new Size(192, 72);
            exportTab.TabIndex = 1;
            exportTab.Text = "Export";
            // 
            // exportPanel
            // 
            exportPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            exportPanel.BackColor = SystemColors.ControlLight;
            exportPanel.Font = new Font("Segoe UI", 9F);
            exportPanel.ForeColor = SystemColors.ControlText;
            exportPanel.Location = new Point(3, 3);
            exportPanel.Margin = new Padding(0);
            exportPanel.MaximumSize = new Size(786, 371);
            exportPanel.MinimumSize = new Size(786, 157);
            exportPanel.Name = "exportPanel";
            exportPanel.Size = new Size(786, 157);
            exportPanel.TabIndex = 14;
            // 
            // resaveTab
            // 
            resaveTab.BackColor = SystemColors.ControlLight;
            resaveTab.Controls.Add(resavePanel);
            resaveTab.Location = new Point(4, 24);
            resaveTab.Margin = new Padding(4, 3, 4, 3);
            resaveTab.Name = "resaveTab";
            resaveTab.Padding = new Padding(4, 3, 4, 3);
            resaveTab.Size = new Size(790, 377);
            resaveTab.TabIndex = 6;
            resaveTab.Text = "Resave";
            // 
            // resavePanel
            // 
            resavePanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            resavePanel.BackColor = SystemColors.ControlLight;
            resavePanel.Font = new Font("Segoe UI", 9F);
            resavePanel.ForeColor = SystemColors.ControlText;
            resavePanel.Location = new Point(3, 3);
            resavePanel.Margin = new Padding(0);
            resavePanel.MaximumSize = new Size(786, 371);
            resavePanel.MinimumSize = new Size(786, 371);
            resavePanel.Name = "resavePanel";
            resavePanel.Size = new Size(786, 371);
            resavePanel.TabIndex = 0;
            // 
            // objectsTab
            // 
            objectsTab.BackColor = SystemColors.ControlLight;
            objectsTab.Controls.Add(objectsPanel);
            objectsTab.Location = new Point(4, 24);
            objectsTab.Margin = new Padding(4, 3, 4, 3);
            objectsTab.Name = "objectsTab";
            objectsTab.Padding = new Padding(4, 3, 4, 3);
            objectsTab.Size = new Size(192, 72);
            objectsTab.TabIndex = 1;
            objectsTab.Text = "Objects";
            // 
            // objectsPanel
            // 
            objectsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            objectsPanel.BackColor = SystemColors.ControlLight;
            objectsPanel.Font = new Font("Segoe UI", 9F);
            objectsPanel.ForeColor = SystemColors.ControlText;
            objectsPanel.Location = new Point(0, 0);
            objectsPanel.Margin = new Padding(0);
            objectsPanel.MaximumSize = new Size(786, 371);
            objectsPanel.MinimumSize = new Size(786, 371);
            objectsPanel.Name = "objectsPanel";
            objectsPanel.Size = new Size(786, 371);
            objectsPanel.TabIndex = 0;
            // 
            // hashTab
            // 
            hashTab.BackColor = SystemColors.ControlLight;
            hashTab.Controls.Add(hashPanel);
            hashTab.Location = new Point(4, 24);
            hashTab.Margin = new Padding(4, 3, 4, 3);
            hashTab.Name = "hashTab";
            hashTab.Padding = new Padding(4, 3, 4, 3);
            hashTab.Size = new Size(192, 72);
            hashTab.TabIndex = 0;
            hashTab.Text = "Hashlist";
            // 
            // hashPanel
            // 
            hashPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            hashPanel.BackColor = SystemColors.ControlLight;
            hashPanel.Font = new Font("Segoe UI", 9F);
            hashPanel.ForeColor = SystemColors.ControlText;
            hashPanel.Location = new Point(3, 3);
            hashPanel.Margin = new Padding(0);
            hashPanel.MaximumSize = new Size(786, 371);
            hashPanel.MinimumSize = new Size(786, 371);
            hashPanel.Name = "hashPanel";
            hashPanel.Size = new Size(786, 371);
            hashPanel.TabIndex = 0;
            // 
            // helpTab
            // 
            helpTab.BackColor = SystemColors.ControlLight;
            helpTab.Controls.Add(helpPanel);
            helpTab.Location = new Point(4, 24);
            helpTab.Margin = new Padding(4, 3, 4, 3);
            helpTab.Name = "helpTab";
            helpTab.Padding = new Padding(4, 3, 4, 3);
            helpTab.Size = new Size(192, 72);
            helpTab.TabIndex = 5;
            helpTab.Text = "Help";
            // 
            // helpPanel
            // 
            helpPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            helpPanel.BackColor = SystemColors.ControlLight;
            helpPanel.Font = new Font("Segoe UI", 9F);
            helpPanel.ForeColor = SystemColors.ControlText;
            helpPanel.Location = new Point(3, 3);
            helpPanel.Margin = new Padding(0);
            helpPanel.MaximumSize = new Size(786, 371);
            helpPanel.MinimumSize = new Size(786, 371);
            helpPanel.Name = "helpPanel";
            helpPanel.Size = new Size(786, 371);
            helpPanel.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(826, 431);
            Controls.Add(mainTabs);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(4, 3, 4, 3);
            MinimumSize = new Size(842, 470);
            Name = "Form1";
            Text = "Diesel Model Tool v1.03";
            Load += Form1_Load;
            mainTabs.ResumeLayout(false);
            importTab.ResumeLayout(false);
            exportTab.ResumeLayout(false);
            resaveTab.ResumeLayout(false);
            objectsTab.ResumeLayout(false);
            hashTab.ResumeLayout(false);
            helpTab.ResumeLayout(false);
            ResumeLayout(false);

        }

        #endregion

        private TabControl mainTabs;
        private TabPage objectsTab;
        private UI.ObjectsPanel objectsPanel;
        private TabPage exportTab;
        private UI.ExportPanel exportPanel;
        private TabPage importTab;
        private UI.ImportPanel importPanel;
        private TabPage hashTab;
        private UI.HashPanel hashPanel;
        private TabPage helpTab;
        private UI.HelpPanel helpPanel;
        private TabPage resaveTab;
        private UI.ResavePanel resavePanel;
    }
}