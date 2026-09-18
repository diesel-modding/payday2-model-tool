using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PD2ModelParser.UI
{
    public partial class HashPanel : UserControl
    {
        private readonly string localHashlistPath = Path.Combine(AppContext.BaseDirectory, "hashlist");

        public HashPanel()
        {
            InitializeComponent();

            // Wire up events to the designer controls
            this.Load += async (_, __) => await OnLoadAsync();
            updateButton.Click += async (_, __) => await FetchButtonClickedAsync();
        }

        private async Task OnLoadAsync()
        {
            if (File.Exists(localHashlistPath))
            {
                _ = new FileInfo(localHashlistPath);
            }
        }

        private async Task FetchButtonClickedAsync()
        {

            var url = textBox1.Text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show(this, "Please enter a URL to fetch the hashlist from.", "No URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            updateButton.Enabled = false;
            try
            {
                await FetchAndSaveHashlist(url);
            }
            finally
            {
                updateButton.Enabled = true;
            }
        }

        private async Task FetchAndSaveHashlist(string url)
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(30);
                var resp = await http.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                var content = await resp.Content.ReadAsStringAsync();

                var dir = Path.GetDirectoryName(localHashlistPath);

                await File.WriteAllTextAsync(localHashlistPath, content);
                StaticStorage.hashindex.RequestReload();

                MessageBox.Show(this, "Hashlist downloaded and saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to download hashlist: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
