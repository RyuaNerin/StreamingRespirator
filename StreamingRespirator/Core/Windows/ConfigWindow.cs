using System;
using System.IO;
using System.Windows.Forms;
using StreamingRespirator.Properties;
using StreamingRespirator.Utilities;

namespace StreamingRespirator.Core.Windows
{
    public partial class ConfigWindow : Form
    {
        public ConfigWindow()
        {
            this.InitializeComponent();
            LocalizationHelper.ApplyLang(this);

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
                MessageBox.Show(this, Lang.ConfigWindow_HttpsWarning, Lang.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

                        shortCut.Description = Lang.Name;
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
                MessageBox.Show(this, Lang.ConfigWindow_ApplyAfterRestart, Lang.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
