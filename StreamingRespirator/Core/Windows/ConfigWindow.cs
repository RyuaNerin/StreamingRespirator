using System;
using System.IO;
using System.Windows.Forms;

namespace StreamingRespirator.Core.Windows
{
    public partial class ConfigWindow : Form
    {
        public ConfigWindow()
        {
            this.InitializeComponent();

            this.ctlAutoStartup.Checked   = Config.StartWithWindows;

            this.ctlPort.Value            = Config.Proxy.Port;
            this.ctlUseHTTPS.Checked      = Config.Proxy.UseHTTPS;

            this.ctlShowRetweet.Checked   = Config.Filter.ShowRetweetedMyStatus;
            this.ctlShowMyRetweet.Checked = Config.Filter.ShowMyRetweet;
        }

        private bool m_loaded = false;
        private void ConfigWindow_Load(object sender, EventArgs e)
        {
            this.m_loaded = true;
        }

        private void ctlUseHTTPS_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.m_loaded)
                return;

            if (this.ctlUseHTTPS.Checked)
            {
                MessageBox.Show(this, "HTTPS 프록시를 사용할 경우 보안에 취약해질 수 있습니다.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            }
        }

        private void ctlOK_Click(object sender, EventArgs e)
        {
            if (Config.StartWithWindows != this.ctlAutoStartup.Checked)
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), Path.GetFileNameWithoutExtension(Application.ExecutablePath) + ".lnk");

                if (!Config.StartWithWindows)
                {
                    try
                    {
                        var ws = new IWshRuntimeLibrary.WshShell();
                        IWshRuntimeLibrary.IWshShortcut shortCut = ws.CreateShortcut(path);

                        shortCut.Description = "스트리밍 호흡기";
                        shortCut.TargetPath = Application.ExecutablePath;
                        shortCut.Save();
                    }
                    catch
                    {
                    }
                }
                else
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                    }
                }
            }

            if (Config.Proxy.Port != (int)this.ctlPort.Value ||
                Config.Proxy.UseHTTPS != this.ctlUseHTTPS.Checked)
            {
                MessageBox.Show(this, "프록시 옵션은 재시작 후 적용됩니다!");
            }

            Config.StartWithWindows             = this.ctlAutoStartup.Checked;


            Config.Proxy.Port                   = (int)this.ctlPort.Value;
            Config.Proxy.UseHTTPS               = this.ctlUseHTTPS.Checked;

            Config.Filter.ShowRetweetedMyStatus = this.ctlShowRetweet.Checked;
            Config.Filter.ShowMyRetweet         = this.ctlShowMyRetweet.Checked;

            Config.Save();

            this.Close();
        }

        private void ctlCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
