using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
                var image = Image.FromFile(AssetPath);
                preview.BackgroundImage = image;
            }
        }

        private void upload_Click(object sender, EventArgs e)
        {
            Upload = true;
            Close();
        }

        private void edit_Click(object sender, EventArgs e)
        {
            Process.Start(AssetPath);
        }

        private void keepLocal_Click(object sender, EventArgs e)
        {
            Upload = false;
            Close();
        }
    }
}
