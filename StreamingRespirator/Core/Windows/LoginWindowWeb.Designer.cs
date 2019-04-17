namespace StreamingRespirator.Core.Windows
{
    partial class LoginWindowWeb
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
            this.ctlWeb = new System.Windows.Forms.WebBrowser();
            this.SuspendLayout();
            // 
            // ctlWeb
            // 
            this.ctlWeb.AllowWebBrowserDrop = false;
            this.ctlWeb.CausesValidation = false;
            this.ctlWeb.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ctlWeb.IsWebBrowserContextMenuEnabled = false;
            this.ctlWeb.Location = new System.Drawing.Point(0, 0);
            this.ctlWeb.MinimumSize = new System.Drawing.Size(20, 20);
            this.ctlWeb.Name = "ctlWeb";
            this.ctlWeb.ScrollBarsEnabled = false;
            this.ctlWeb.Size = new System.Drawing.Size(387, 241);
            this.ctlWeb.TabIndex = 0;
            this.ctlWeb.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(this.ctlWeb_DocumentCompleted);
            this.ctlWeb.Navigating += new System.Windows.Forms.WebBrowserNavigatingEventHandler(this.ctlWeb_Navigating);
            // 
            // LoginWindowWeb
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(387, 241);
            this.Controls.Add(this.ctlWeb);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LoginWindowWeb";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "스트리밍 호흡기 로그인";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.LoginWindow2_FormClosed);
            this.Load += new System.EventHandler(this.LoginWindow2_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.WebBrowser ctlWeb;
    }
}
