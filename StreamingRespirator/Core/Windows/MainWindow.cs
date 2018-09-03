using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using StreamingRespirator.Core.CefHelper;
using StreamingRespirator.Core.Streaming;
using StreamingRespirator.Utilities;

namespace StreamingRespirator.Core.Windows
{
    internal partial class MainWindow : Form
    {
        private readonly RespiratorServer     m_server;
        private readonly ChromeRequestHandler m_chromeReqeustHandler;
        private readonly ChromiumWebBrowser   m_browser;

        private bool m_authorized = false;

        public MainWindow()
        {
            InitializeComponent();

            this.m_server = new RespiratorServer();

            this.m_chromeReqeustHandler = new ChromeRequestHandler();
            this.m_chromeReqeustHandler.TweetdeckAuthorized += this.Twitter_TweetdeckAuthorized;
            this.m_chromeReqeustHandler.TwitterApiRersponse += this.Twitter_TwitterApiRersponse;

            this.m_browser = new ChromiumWebBrowser("")
            {
                Dock            = DockStyle.Fill,
                BrowserSettings = Program.DefaultBrowserSetting,
                RequestHandler  = this.m_chromeReqeustHandler,
                LifeSpanHandler = new LifeSpanHandler(),
            };
            this.m_browser.FrameLoadEnd += this.Browser_FrameLoadEnd;
            this.m_browser.IsBrowserInitializedChanged += this.M_browser_IsBrowserInitializedChanged;
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            
            if (await Task.Factory.StartNew(GithubLatestRelease.CheckNewVersion))
            {
                MessageBox.Show(this, "새로운 업데이트가 있습니다.");

                try
                {
                    Process.Start("https://github.com/RyuaNerin/StreamingRespirator/blob/master/README.md")?.Dispose();
                }
                catch
                {
                }

                Application.Exit();
                return;
            }

            this.Controls.Add(this.m_browser);

        }

        private void M_browser_IsBrowserInitializedChanged(object sender, IsBrowserInitializedChangedEventArgs e)
        {
            if (e.IsBrowserInitialized)
            {
                //this.m_browser.ShowDevTools();
                this.m_browser.Load("https://tweetdeck.twitter.com/");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (this.m_authorized && e.CloseReason == CloseReason.UserClosing)
            {
                this.Hide();
                e.Cancel = true;

                return;
            }

            this.ntf.Visible = false;
        }

        private void Browser_FrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if (Uri.TryCreate(e.Url, UriKind.Absolute, out var uri) && !uri.Host.Contains("twitter.com"))
                e.Frame.LoadUrl("https://tweetdeck.twitter.com/");
        }

        private void Twitter_TweetdeckAuthorized()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(this.Twitter_TweetdeckAuthorized));
                return;
            }

            this.m_server.Start();

            this.Text = $"{this.Text} - Port {this.m_server.ProxyPort}";
            this.ntf.Text = this.Text;

            this.ntf.Text = this.Text;
            this.ntf.Icon = this.Icon;
            this.ntf.Visible = true;

            this.m_authorized = true;
        }

        private void Twitter_TwitterApiRersponse(TwitterApiResponse response)
        {
            this.m_server.AddApiResponse(response);
        }

        private void byRyuaNerinToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start("https://ryuanerin.kr")?.Dispose();
            }
            catch
            {
            }
        }

        private void 열기ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
        }

        private void 닫기ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void azureaPatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool b = false;
            foreach (var process in Process.GetProcessesByName("azurea"))
                using (process)
                    b |= Hook.HookWinInet(this.m_server.ProxyPort, process);

            if (b)
            {
                this.Focus();
                MessageBox.Show(this, "아즈레아에 스트리밍 호흡기를 적용하였습니다.");
            }
        }

        private void ntf_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                this.Show();
        }
    }
}
