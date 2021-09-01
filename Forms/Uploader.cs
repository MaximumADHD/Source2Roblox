using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Source2Roblox.Forms
{
    public partial class Uploader : Form
    {
        public readonly string AssetPath;
        public bool Upload { get; private set; }

        public Uploader(string assetPath)
        {
            var info = new FileInfo(assetPath);
            InitializeComponent();
            AssetPath = assetPath;

            if (!info.Exists)
            {
                Upload = false;
                Close();

                return;
            }

            if (Program.NO_PROMPT)
            {
                Console.WriteLine($"-noPrompt requested in upload of {assetPath}, proceeding...");
                Upload = true;

                Close();
                return;
            }

            if (info.Extension == ".png")
            {
                var dir = info.DirectoryName;
                var fileName = info.Name;

                var watcher = new FileSystemWatcher(dir, fileName);
                watcher.Changed += new FileSystemEventHandler(RefreshPreview);

                edit.Enabled = true;
                RefreshPreview();
            }

            Console.WriteLine($"Prompting user to upload: {assetPath}");
        }

        public void RefreshPreview(object sender = null, FileSystemEventArgs e = null)
        {
            var info = new FileInfo(AssetPath);

            if (info.Extension == ".png")
            {
                using (var stream = File.OpenRead(AssetPath))
                {
                    var image = Image.FromStream(stream);
                    preview.BackgroundImage = image;
                }
            }
        }

        private void OnUploadClicked(object sender, EventArgs e)
        {
            Upload = true;
            Close();
        }

        private void OnEditClicked(object sender, EventArgs e)
        {
            Process.Start(AssetPath);
        }

        private void OnKeepLocalClicked(object sender, EventArgs e)
        {
            Upload = false;
            Close();
        }
    }
}
