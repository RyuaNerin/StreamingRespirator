namespace StreamingRespirator.Core.Windows
{
    partial class MainWindow
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.cms = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.localhost8080ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.byRyuaNerinToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.azureaPatchToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.열기ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.닫기ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ntf = new System.Windows.Forms.NotifyIcon(this.components);
            this.cms.SuspendLayout();
            this.SuspendLayout();
            // 
            // cms
            // 
            this.cms.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.localhost8080ToolStripMenuItem,
            this.byRyuaNerinToolStripMenuItem,
            this.toolStripSeparator2,
            this.azureaPatchToolStripMenuItem,
            this.toolStripSeparator1,
            this.열기ToolStripMenuItem,
            this.닫기ToolStripMenuItem});
            this.cms.Name = "cms";
            this.cms.Size = new System.Drawing.Size(181, 148);
            // 
            // localhost8080ToolStripMenuItem
            // 
            this.localhost8080ToolStripMenuItem.Name = "localhost8080ToolStripMenuItem";
            this.localhost8080ToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.localhost8080ToolStripMenuItem.Text = "localhost / 8080";
            // 
            // byRyuaNerinToolStripMenuItem
            // 
            this.byRyuaNerinToolStripMenuItem.Name = "byRyuaNerinToolStripMenuItem";
            this.byRyuaNerinToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.byRyuaNerinToolStripMenuItem.Text = "By RyuaNerin";
            this.byRyuaNerinToolStripMenuItem.Click += new System.EventHandler(this.byRyuaNerinToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(177, 6);
            // 
            // azureaPatchToolStripMenuItem
            // 
            this.azureaPatchToolStripMenuItem.Name = "azureaPatchToolStripMenuItem";
            this.azureaPatchToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.azureaPatchToolStripMenuItem.Text = "OneClick - Azurea";
            this.azureaPatchToolStripMenuItem.Click += new System.EventHandler(this.azureaPatchToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(177, 6);
            // 
            // 열기ToolStripMenuItem
            // 
            this.열기ToolStripMenuItem.Name = "열기ToolStripMenuItem";
            this.열기ToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.열기ToolStripMenuItem.Text = "열기";
            this.열기ToolStripMenuItem.Click += new System.EventHandler(this.열기ToolStripMenuItem_Click);
            // 
            // 닫기ToolStripMenuItem
            // 
            this.닫기ToolStripMenuItem.Name = "닫기ToolStripMenuItem";
            this.닫기ToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.닫기ToolStripMenuItem.Text = "종료";
            this.닫기ToolStripMenuItem.Click += new System.EventHandler(this.닫기ToolStripMenuItem_Click);
            // 
            // ntf
            // 
            this.ntf.ContextMenuStrip = this.cms;
            this.ntf.Visible = true;
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(620, 438);
            this.Name = "MainWindow";
            this.Text = "스트리밍호흡기";
            this.cms.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip cms;
        private System.Windows.Forms.NotifyIcon ntf;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem byRyuaNerinToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 열기ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 닫기ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem localhost8080ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem azureaPatchToolStripMenuItem;
    }
}

