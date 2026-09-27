using PD2ModelParser.Exporters;
using PD2ModelParser.Importers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using PD2ModelParser.Sections;

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

            string directory = Path.GetDirectoryName(inputPath) ?? string.Empty;
            string fileName = Path.GetFileNameWithoutExtension(inputPath);
            string outputPath = Path.Combine(directory, fileName + ".new.model");

            try
            {
                FullModelData model = ModelReader.Open(inputPath);

                List<string> unknownDataWarnings = GetUnknownDataWarnings(model);
                if (unknownDataWarnings.Count > 0)
                {
                    string warningDetails = string.Join("\n", unknownDataWarnings);
                    DialogResult result = MessageBox.Show(
                        "The model contains data that is not fully understood by the parser:\n\n" +
                        warningDetails +
                        "\n\nResaving may not preserve the meaning of this data exactly. Proceed anyway?",
                        "Unknown model data",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);

                    if (result != DialogResult.Yes)
                        return;
                }

                DieselExporter.ExportFile(model, outputPath);

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

        private static List<string> GetUnknownDataWarnings(FullModelData model)
        {
            List<string> warnings = [];

            foreach (var pair in model.parsed_sections)
            {
                if (pair.Value is Unknown unknown)
                {
                    warnings.Add($"• Section {pair.Key}: unknown type 0x{unknown.TypeCode:X8} ({unknown.data?.Length ?? 0} bytes)");
                }
            }

            return warnings;
        }
    }
}
