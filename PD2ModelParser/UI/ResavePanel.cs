using PD2ModelParser.Modelscript;
using System;
using System.IO;
using System.Windows.Forms;

namespace PD2ModelParser.UI
{
    public partial class ResavePanel : UserControl
    {
        public ResavePanel()
        {
            InitializeComponent();

            inputFileBox.Filter = "Diesel Model Files (*.model)|*.model";
            inputFileBox.FileSelected += InputFileBox_FileSelected;
        }

        private void InputFileBox_FileSelected(object sender, EventArgs e)
        {
            exportBttn.Enabled = inputFileBox.Selected != null;
        }

        private void ExportBttn_Click(object sender, EventArgs e)
        {
            string inputPath = inputFileBox.Selected;
            if (inputPath == null)
                return;

            try
            {
                string outputPath = Resave.ResaveFile(inputPath, overwrite: overwriteCheckBox.Checked, confirmUnknownData: warnings =>
                {
                    DialogResult result = MessageBox.Show(
                        "The model contains data that is not fully understood by the parser:\n\n" +
                        "• " + string.Join("\n• ", warnings) +
                        "\n\nResaving may not preserve the meaning of this data exactly. Proceed anyway?",
                        "Unknown model data",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);

                    return result == DialogResult.Yes;
                });

                if (outputPath == null)
                    return;

                MessageBox.Show(
                    $"Successfully resaved model as {Path.GetFileName(outputPath)} (placed in the input model folder)",
                    "Resave complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log.Default.Error("Failed to resave model {0}: {1}", inputPath, ex);
                MessageBox.Show(
                    $"Failed to resave model:\n{ex.Message}",
                    "Resave failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
