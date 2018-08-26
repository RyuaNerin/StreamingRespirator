using System;
using System.Diagnostics;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

namespace StreamingRespirator.Core.Windows
{
    internal partial class MainWindow : Form
    {
        private readonly RespiratorServer     m_server;
        private readonly ChromeRequestHandler m_chromeReqeustHandler;
        private readonly ChromiumWebBrowser   m_browser;

        public MainWindow()
        {
            InitializeComponent();

            this.m_server = new RespiratorServer();

            this.m_chromeReqeustHandler = new ChromeRequestHandler();
            this.m_chromeReqeustHandler.TwitterApiRersponse += this.Twitter_TwitterApiRersponse;

            this.m_browser = new ChromiumWebBrowser("https://tweetdeck.twitter.com/")
            {
                Dock = DockStyle.Fill,
                BrowserSettings =
                {
                    DefaultEncoding           = "UTF-8",
                    WebGl                     = CefState.Disabled,
                    Plugins                   = CefState.Disabled,
                    JavascriptAccessClipboard = CefState.Disabled,
                    ImageLoading              = CefState.Disabled,
                    JavascriptCloseWindows    = CefState.Disabled,
                    ApplicationCache          = CefState.Enabled,
                    RemoteFonts               = CefState.Disabled,
                    WindowlessFrameRate       = 1,
                },
                RequestHandler = this.m_chromeReqeustHandler
            };
            this.m_browser.FrameLoadEnd += this.M_browser_FrameLoadEnd;

            this.Controls.Add(this.m_browser);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.m_server.Start();

            this.Text = $"{this.Text} - Port {this.m_server.ProxyPort}";
            this.localhost8080ToolStripMenuItem.Text = $"localhost / port: {this.m_server.ProxyPort}";

            this.ntf.Text = this.Text;
            this.ntf.Icon = this.Icon;
            this.ntf.Visible = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (e.CloseReason == CloseReason.UserClosing)
            {
                this.Hide();
                e.Cancel = true;

                return;
            }

            this.ntf.Visible = false;
        }

        private void Twitter_TwitterApiRersponse(TwitterApiResponse response)
        {
            this.m_server.AddApiResponse(response);
        }

        private void M_browser_FrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if (Uri.TryCreate(e.Url, UriKind.Absolute, out var uri) && !uri.Host.Contains("twitter.com"))
                e.Frame.LoadUrl("https://tweetdeck.twitter.com/");
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
                MessageBox.Show(this, "아즈레아에 스트리밍 호흡기를 적용하였습니다.");
        }
    }
}
