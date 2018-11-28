using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CefSharp;
using CefSharp.OffScreen;
using StreamingRespirator.Core.CefHelper;
using StreamingRespirator.Core.Streaming;
using StreamingRespirator.Core.Windows;
using StreamingRespirator.Utilities;

namespace StreamingRespirator.Core
{
    internal class MainContext : ApplicationContext
    {
        private readonly RespiratorServer m_server;
        private readonly ChromeRequestHandler m_chromeReqeustHandler;
        private readonly ChromiumWebBrowser m_browser;

        private readonly Control m_control;

        private NotifyIcon         m_notifyIcon;
        private ContextMenuStrip   m_contextMenuStrip;
        private ToolStripLabel     m_stripPort;
        private ToolStripMenuItem  m_stripAbout;
        private ToolStripSeparator m_stripSep0;
        private ToolStripMenuItem  m_stripCredentials;
        private ToolStripMenuItem  m_stripRefresh;
        private ToolStripSeparator m_stripSep1;
        private ToolStripMenuItem  m_scripHookAzurea;
        private ToolStripSeparator m_scripSep2;
        private ToolStripMenuItem  m_stripExit;

        public MainContext()
        {
            this.m_control = new Control();
            this.m_control.CreateControl();

            this.InitializeComponent();

            CefSharpSettings.SubprocessExitIfParentProcessClosed = true;
            CefSharpSettings.ShutdownOnExit = true;
            CefSharpSettings.WcfEnabled = false;
            CefSharpSettings.Proxy = null;

            Cef.Initialize(Program.DefaultCefSetting, false, null);
            Cef.EnableHighDPISupport();

            Cef.GetGlobalCookieManager().SetStoragePath(Program.CookiePath, true, null);

            this.m_server = new RespiratorServer();

            this.m_chromeReqeustHandler = new ChromeRequestHandler();
            this.m_chromeReqeustHandler.TweetdeckAuthorized += this.Twitter_TweetdeckAuthorized;
            this.m_chromeReqeustHandler.TwitterApiRersponse += this.Twitter_TwitterApiRersponse;
            this.m_chromeReqeustHandler.ColumnsUpdated += this.Twitter_ColumnsUpdated;

            this.m_browser = new ChromiumWebBrowser("https://tweetdeck.twitter.com/", Program.DefaultBrowserSetting)
            {
                RequestHandler = this.m_chromeReqeustHandler,
                LifeSpanHandler = new LifeSpanHandler(),
            };
            this.m_browser.LoadingStateChanged += this.Browser_LoadingStateChanged;
            this.m_browser.ConsoleMessage += this.Browser_ConsoleMessage;
        }

        private void InitializeComponent()
        {
            this.m_stripPort = new ToolStripLabel("Port : ");

            this.m_stripAbout = new ToolStripMenuItem("By RyuaNerin");
            this.m_stripAbout.Click += this.StripAbout_Click;

            this.m_stripSep0 = new ToolStripSeparator();

            this.m_stripCredentials = new ToolStripMenuItem("활성화된 아이디");
            this.m_stripCredentials.Enabled = false;
            (this.m_stripCredentials.DropDown as ToolStripDropDownMenu).ShowImageMargin = false;

            this.m_stripRefresh = new ToolStripMenuItem("새로고침");
            this.m_stripRefresh.Click += this.StripRefresh_Click;

            this.m_stripSep1 = new ToolStripSeparator();

            this.m_scripHookAzurea = new ToolStripMenuItem("아즈레아 바로 적용");
            this.m_scripHookAzurea.Click += this.StripHookAzurea_Click;

            this.m_scripSep2 = new ToolStripSeparator();

            this.m_stripExit = new ToolStripMenuItem("종료");
            this.m_stripExit.Click += this.StripExit_Click;

            this.m_contextMenuStrip = new ContextMenuStrip
            {
                RenderMode      = ToolStripRenderMode.Professional,
                ShowImageMargin = false,
                Items =
                {
                    this.m_stripPort,
                    this.m_stripAbout,
                    this.m_stripSep0,
                    this.m_stripCredentials,
                    this.m_stripRefresh,
                    this.m_stripSep1,
                    this.m_scripHookAzurea,
                    this.m_scripSep2,
                    this.m_stripExit,
                },
            };

            this.m_notifyIcon = new NotifyIcon
            {
                Icon             = Properties.Resources.tray,
                ContextMenuStrip = this.m_contextMenuStrip,
                Visible          = true,
            };
        }

        public void StopProxy()
        {
            this.m_server.Stop();

            this.m_notifyIcon.Visible = false;
        }

        private void Twitter_TweetdeckAuthorized(bool logined)
        {
            if (this.m_control.InvokeRequired)
            {
                this.m_control.Invoke(new Action<bool>(this.Twitter_TweetdeckAuthorized), logined);
                return;
            }

            if (logined)
            {
                Debug.WriteLine("server start");
                this.m_server.Start();

                Debug.WriteLine("proxy port : " + this.m_server.ProxyPort);
                this.m_stripPort.Text = $"Port : {this.m_server.ProxyPort}";

                this.m_notifyIcon.Text = $"스트리밍 호흡기 - Port {this.m_server.ProxyPort}";
                this.m_notifyIcon.Visible = true;
            }
            else
            {
                Debug.WriteLine("navigate to login");

                this.m_browser.Load("https://twitter.com/login?hide_message=true&redirect_after_login=https%3A%2F%2Ftweetdeck.twitter.com%2F%3Fvia_twitter_login%3Dtrue");
            }
        }

        private void Twitter_ColumnsUpdated(ColumnInfo[] columns)
        {
            if (this.m_control.InvokeRequired)
            {
                this.m_control.Invoke(new Action<ColumnInfo[]>(this.Twitter_ColumnsUpdated), columns);
                return;
            }

            this.m_stripCredentials.Enabled = columns.Length != 0;

            var oldItem = new ToolStripItem[this.m_stripCredentials.DropDownItems.Count];
            this.m_stripCredentials.DropDownItems.CopyTo(oldItem, 0);
            this.m_stripCredentials.DropDownItems.Clear();

            foreach (var item in oldItem)
                item.Dispose();

            var gc = columns.GroupBy(e => e.Description).ToArray();
            for (int i = 0; i < gc.Length; ++i)
            {
                foreach (var column in gc[i])
                {
                    Image img = null;
                    switch (column.ColumnType)
                    {
                        case ColumnTypes.HomeTimeline:  img = Properties.Resources.uniF053; break;
                        case ColumnTypes.Notification:  img = Properties.Resources.uniF055; break;
                        case ColumnTypes.Activity:      img = Properties.Resources.uniF063; break;
                        case ColumnTypes.DirectMessage: img = Properties.Resources.uniF054; break;
                    }

                    this.m_stripCredentials.DropDownItems.Add(new ToolStripLabel(column.Description, img));
                }

                if (i != gc.Length - 1)
                    this.m_stripCredentials.DropDownItems.Add(new ToolStripSeparator());
            }
        }

        private void Twitter_TwitterApiRersponse(TwitterApiResponse response)
        {
            this.m_server.AddApiResponse(response);
        }

        private void Browser_ConsoleMessage(object sender, ConsoleMessageEventArgs e)
        {
            Debug.WriteLine("ConsoleMessage " + e.Message);
        }

        private void Browser_LoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
        {
            Debug.WriteLine("LoadingStateChanged " + e.Browser.MainFrame.Url);

            if (e.IsLoading)
                return;

            if (!Uri.TryCreate(e.Browser.MainFrame.Url, UriKind.Absolute, out var uri))
                return;

            if ((uri.Host == "twitter.com" || uri.Host == "www.twitter.com") &&
                 uri.AbsolutePath.Contains("login"))
            {
                this.m_browser.ExecuteScriptAsync("console.log(document.getElementsByClassName('message-text')[0].innerText);");

                // 에러띄우기
                var esa = this.m_browser.EvaluateScriptAsync("(function() { return document.getElementsByClassName('message-text')[0].innerText; })()", TimeSpan.FromSeconds(1));
                try
                {
                    esa.Wait();
                    if (esa.Result.Success)
                    {
                        var err_msg = esa.Result.Result as string;
                        if (string.IsNullOrWhiteSpace(err_msg))
                            MessageBox.Show(err_msg, "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
                catch
                { }

                string username, password;
                using (var frm = new LoginWindow())
                {
                    if (frm.ShowDialog() != DialogResult.OK)
                    {
                        Application.Exit();
                        return;
                    }

                    username = frm.Username;
                    password = frm.Password;
                }

                var js =
                    $"(function() {{" +
                    $"    var form = document.getElementsByClassName('signin-wrapper')[0].getElementsByTagName('form')[0];" +
                    $"    console.log(form);" +
                    $"    form.getElementsByClassName('js-username-field')[0].value = '{username.Replace("'", "\\'")}';" +
                    $"    form.getElementsByClassName('js-password-field')[0].value = '{password.Replace("'", "\\'")}';" +
                    $"    form.submit();" +
                    $"}})();";

                e.Browser.MainFrame.ExecuteJavaScriptAsync(js);
            }
        }

        private void StripAbout_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start("https://ryuanerin.kr")?.Dispose();
            }
            catch
            {
            }
        }

        private void StripRefresh_Click(object sender, EventArgs e)
        {
            this.m_browser.Load("https://tweetdeck.twitter.com/");
        }

        private void StripExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void StripHookAzurea_Click(object sender, EventArgs e)
        {
            bool b = false;
            foreach (var process in Process.GetProcessesByName("azurea"))
                using (process)
                    b |= Hook.HookWinInet(this.m_server.ProxyPort, process);

            if (b)
            {
                MessageBox.Show("아즈레아에 스트리밍 호흡기를 적용하였습니다.", "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
