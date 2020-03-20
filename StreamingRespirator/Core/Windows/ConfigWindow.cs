using System;
using System.IO;
using System.Windows.Forms;
using Sentry;

namespace StreamingRespirator.Core.Windows
{
    public partial class ConfigWindow : Form
    {
        public ConfigWindow()
        {
            this.InitializeComponent();
            Lang.ApplyLang(this);

            lock (Config.Instance.Lock)
            {
                this.ctlAutoStartup.Checked = Config.Instance.StartWithWindows;

                this.ctlPort.Value = Config.Instance.Proxy.Port;
                this.ctlAuthId.Text = Config.Instance.Proxy.Id;
                this.ctlAuthPw.Text = Config.Instance.Proxy.Pw;

                this.ctlReduceApiCall.Checked = Config.Instance.ReduceApiCall;

                this.ctlShowRetweet.Checked = Config.Instance.Filter.ShowRetweetedMyStatus;
                this.ctlShowRetweetWithComment.Checked = Config.Instance.Filter.ShowRetweetWithComment;
                this.ctlShowMyRetweet.Checked = Config.Instance.Filter.ShowMyRetweet;
            }
        }

        private void ctlOK_Click(object sender, EventArgs e)
        {
            lock (Config.Instance.Lock)
            {
                if (Config.Instance.StartWithWindows != this.ctlAutoStartup.Checked)
                {
                    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), Path.GetFileNameWithoutExtension(Application.ExecutablePath) + ".lnk");

                    if (!Config.Instance.StartWithWindows)
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

                Config.Instance.StartWithWindows = this.ctlAutoStartup.Checked;

                Config.Instance.Proxy.Port = (int)this.ctlPort.Value;
                Config.Instance.Proxy.Id = this.ctlAuthId.Text;
                Config.Instance.Proxy.Pw = this.ctlAuthPw.Text;

                Config.Instance.ReduceApiCall = this.ctlReduceApiCall.Checked;

                Config.Instance.Filter.ShowRetweetedMyStatus = this.ctlShowRetweet.Checked;
                Config.Instance.Filter.ShowRetweetWithComment = this.ctlShowRetweetWithComment.Checked;
                Config.Instance.Filter.ShowMyRetweet = this.ctlShowMyRetweet.Checked;
            }

            Config.Save();

            this.Close();
        }

        private void ctlCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
