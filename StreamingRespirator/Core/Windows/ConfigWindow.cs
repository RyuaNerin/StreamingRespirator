using System;
using System.IO;
using System.Windows.Forms;
using Sentry;
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

            this.ctlAutoStartup.Checked = Config.StartWithWindows;

            this.ctlPort.Value = Config.Proxy.Port;

            this.ctlReduceApiCall.Checked = Config.ReduceApiCall;

            this.ctlShowRetweet.Checked = Config.Filter.ShowRetweetedMyStatus;
            this.ctlShowRetweetWithComment.Checked = Config.Filter.ShowRetweetWithComment;
            this.ctlShowMyRetweet.Checked = Config.Filter.ShowMyRetweet;
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
                    catch (Exception ex)
                    {
                        SentrySdk.CaptureException(ex);
                    }
                }
                else
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        SentrySdk.CaptureException(ex);
                    }
                }
            }

            if (Config.Proxy.Port != (int)this.ctlPort.Value)
            {
                MessageBox.Show(this, Lang.ConfigWindow_ApplyAfterRestart, Lang.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            Config.StartWithWindows = this.ctlAutoStartup.Checked;

            Config.Proxy.Port = (int)this.ctlPort.Value;

            Config.ReduceApiCall = this.ctlReduceApiCall.Checked;

            Config.Filter.ShowRetweetedMyStatus = this.ctlShowRetweet.Checked;
            Config.Filter.ShowRetweetWithComment = this.ctlShowRetweetWithComment.Checked;
            Config.Filter.ShowMyRetweet = this.ctlShowMyRetweet.Checked;

            Config.Save();

            this.Close();
        }

        private void ctlCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
