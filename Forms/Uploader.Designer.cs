
namespace Source2Roblox.Forms
{
    partial class Uploader
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
            this.description = new System.Windows.Forms.Label();
            this.preview = new System.Windows.Forms.PictureBox();
            this.title = new System.Windows.Forms.Label();
            this.upload = new System.Windows.Forms.Button();
            this.edit = new System.Windows.Forms.Button();
            this.keepLocal = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.preview)).BeginInit();
            this.SuspendLayout();
            // 
            // description
            // 
            this.description.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.description.Location = new System.Drawing.Point(12, 69);
            this.description.Margin = new System.Windows.Forms.Padding(0, 0, 0, 16);
            this.description.Name = "description";
            this.description.Size = new System.Drawing.Size(554, 185);
            this.description.TabIndex = 0;
            this.description.Text = "The following asset will be uploaded to the account you are signed into from Robl" +
    "ox Studio.\r\n\r\nIt may be subject to moderation.\r\nDo you consent to uploading this" +
    "?";
            this.description.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // preview
            // 
            this.preview.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.preview.Location = new System.Drawing.Point(12, 256);
            this.preview.Name = "preview";
            this.preview.Size = new System.Drawing.Size(554, 379);
            this.preview.TabIndex = 1;
            this.preview.TabStop = false;
            // 
            // title
            // 
            this.title.Font = new System.Drawing.Font("Segoe UI Black", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.title.Location = new System.Drawing.Point(12, 9);
            this.title.Margin = new System.Windows.Forms.Padding(0, 0, 0, 16);
            this.title.Name = "title";
            this.title.Size = new System.Drawing.Size(554, 63);
            this.title.TabIndex = 2;
            this.title.Text = "WARNING:";
            this.title.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // upload
            // 
            this.upload.Location = new System.Drawing.Point(12, 653);
            this.upload.Name = "upload";
            this.upload.Size = new System.Drawing.Size(553, 47);
            this.upload.TabIndex = 3;
            this.upload.Text = "Upload";
            this.upload.UseVisualStyleBackColor = true;
            this.upload.Click += new System.EventHandler(this.OnUploadClicked);
            // 
            // edit
            // 
            this.edit.Enabled = false;
            this.edit.Location = new System.Drawing.Point(12, 706);
            this.edit.Name = "edit";
            this.edit.Size = new System.Drawing.Size(553, 47);
            this.edit.TabIndex = 4;
            this.edit.Text = "Edit";
            this.edit.UseVisualStyleBackColor = true;
            this.edit.Click += new System.EventHandler(this.OnEditClicked);
            // 
            // keepLocal
            // 
            this.keepLocal.Location = new System.Drawing.Point(12, 759);
            this.keepLocal.Name = "keepLocal";
            this.keepLocal.Size = new System.Drawing.Size(553, 47);
            this.keepLocal.TabIndex = 5;
            this.keepLocal.Text = "Keep Local";
            this.keepLocal.UseVisualStyleBackColor = true;
            this.keepLocal.Click += new System.EventHandler(this.OnKeepLocalClicked);
            // 
            // Uploader
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(578, 832);
            this.Controls.Add(this.keepLocal);
            this.Controls.Add(this.edit);
            this.Controls.Add(this.upload);
            this.Controls.Add(this.title);
            this.Controls.Add(this.preview);
            this.Controls.Add(this.description);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Uploader";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Uploader";
            ((System.ComponentModel.ISupportInitialize)(this.preview)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label description;
        private System.Windows.Forms.PictureBox preview;
        private System.Windows.Forms.Label title;
        private System.Windows.Forms.Button upload;
        private System.Windows.Forms.Button edit;
        private System.Windows.Forms.Button keepLocal;
    }
}