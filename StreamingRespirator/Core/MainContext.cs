using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
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
        private static readonly IDictionary<int, Image> ColImages = new SortedDictionary<int, Image>
        {
            { 0, Properties.Resources.Cols_0_min },
            { 1, Properties.Resources.Cols_1_min },
            { 2, Properties.Resources.Cols_2_min },
            { 3, Properties.Resources.Cols_3_min },
            { 4, Properties.Resources.Cols_4_min },
            { 5, Properties.Resources.Cols_5_min },
            { 6, Properties.Resources.Cols_6_min },
            { 7, Properties.Resources.Cols_7_min },
        };

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

#if DEBUG
            this.m_browser.AddressChanged     += (s, e) => Debug.WriteLine("AddressChanged : " + e.Address);
            this.m_browser.BrowserInitialized += (s, e) => Debug.WriteLine("BrowserInitialized");
            this.m_browser.ConsoleMessage     += (s, e) => Debug.WriteLine("ConsoleMessage : " + e.Message);
            this.m_browser.FrameLoadEnd       += (s, e) => Debug.WriteLine("FrameLoadEnd : " + e.Url);
            this.m_browser.FrameLoadStart     += (s, e) => Debug.WriteLine("FrameLoadStart : " + e.Url);
            this.m_browser.LoadError          += (s, e) => Debug.WriteLine("LoadError : " + e.ErrorText);
            this.m_browser.Paint              += (s, e) => Debug.WriteLine("Paint : " + e.DirtyRect.ToString());
            this.m_browser.StatusMessage      += (s, e) => Debug.WriteLine("StatusMessage : " + e.Value);
            this.m_browser.TitleChanged       += (s, e) => Debug.WriteLine("TitleChanged : " + e.Title);
#endif
        }

        private void InitializeComponent()
        {
            this.m_stripPort = new ToolStripLabel("Port : ");

            this.m_stripAbout = new ToolStripMenuItem("By RyuaNerin");
            this.m_stripAbout.Click += this.StripAbout_Click;

            this.m_stripSep0 = new ToolStripSeparator();

            this.m_stripCredentials = new ToolStripMenuItem("활성화된 아이디")
            {
                Enabled = false
            };
            (this.m_stripCredentials.DropDown as ToolStripDropDownMenu).ShowImageMargin = false;
            (this.m_stripCredentials.DropDown as ToolStripDropDownMenu).ImageScalingSize = new Size(32 * 3, 32);

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

        private async void Twitter_TweetdeckAuthorized(bool logined)
        {
            if (this.m_control.InvokeRequired)
            {
                this.m_control.BeginInvoke(new Action<bool>(this.Twitter_TweetdeckAuthorized), logined);
                return;
            }

            if (logined)
            {
                await Task.Factory.StartNew(new Action(this.m_server.Start));
                
                this.m_stripPort.Text = $"Port : {this.m_server.ProxyPort}";

                this.m_notifyIcon.Text = $"스트리밍 호흡기 - Port {this.m_server.ProxyPort}";
                this.m_notifyIcon.Visible = true;
            }
            else
            {
                this.m_browser.Load("https://twitter.com/login?hide_message=true&redirect_after_login=https%3A%2F%2Ftweetdeck.twitter.com%2F%3Fvia_twitter_login%3Dtrue");
            }
        }

        private void Twitter_ColumnsUpdated(ColumnInfo[] columns)
        {
            if (this.m_control.InvokeRequired)
            {
                this.m_control.BeginInvoke(new Action<ColumnInfo[]>(this.Twitter_ColumnsUpdated), columns);
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
                var imgIndex = 0;

                if (gc[i].Any(e => e.ColumnType == ColumnTypes.HomeTimeline))  imgIndex += 4;
                if (gc[i].Any(e => e.ColumnType == ColumnTypes.Notification))  imgIndex += 2;
                if (gc[i].Any(e => e.ColumnType == ColumnTypes.DirectMessage)) imgIndex += 1;

                this.m_stripCredentials.DropDownItems.Add(new ToolStripLabel(gc[i].Key));
                this.m_stripCredentials.DropDownItems.Add(new ToolStripLabel(ColImages[imgIndex]) { ImageScaling = ToolStripItemImageScaling.None });

                if (i != gc.Length - 1)
                    this.m_stripCredentials.DropDownItems.Add(new ToolStripSeparator());
            }
        }

        private void Twitter_TwitterApiRersponse(TwitterApiResponse response)
        {
            this.m_server.AddApiResponse(response);
        }

        private bool m_submitted = false;
        private void Browser_LoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
        {
            Debug.WriteLine($"LoadingStateChanged {e.IsLoading} {e.Browser.MainFrame.Url}");

            if (e.IsLoading)
                return;

            if (!Uri.TryCreate(e.Browser.MainFrame.Url, UriKind.Absolute, out var uri))
                return;

            if ((uri.Host == "twitter.com" || uri.Host == "www.twitter.com") &&
                (uri.AbsolutePath == "/login" || uri.AbsolutePath == "/login/error"))
            {
                if (this.m_submitted)
                {
                    this.m_submitted = false;
                    return;
                }

                Task.Factory.StartNew(new Action(this.StartLogin));
            }
        }

        private void StartLogin()
        {
            // 에러띄우기
            var esa = this.m_browser.EvaluateScriptAsync("(function() { return document.getElementsByClassName('message-text')[0].innerText; })()", TimeSpan.FromSeconds(1));
            try
            {
                esa.Wait();
                if (esa.Result.Success)
                {
                    var err_msg = esa.Result.Result as string;
                    if (!string.IsNullOrWhiteSpace(err_msg))
                        MessageBox.Show(err_msg, "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            catch
            { }

            string username = null, password = null;
            if ((bool)this.m_control.Invoke(new Func<bool>(
                () =>
                {
                    using (var frm = new LoginWindow())
                    {
                        if (frm.ShowDialog() != DialogResult.OK)
                            return true;

                        username = frm.Username;
                        password = frm.Password;

                        return false;
                    }
                })))
            {
                Application.Exit();
                return;
            }

            var js = $@"
                (function() {{
                    var form = document.getElementsByClassName('signin-wrapper')[0].getElementsByTagName('form')[0];
                    form.getElementsByClassName('js-username-field')[0].value = '{username.Replace("'", "\\'")}';
                    form.getElementsByClassName('js-password-field')[0].value = '{password.Replace("'", "\\'")}';
                    setTimeout(function() {{ form.submit(); }}, 1000);
                }})();";

            this.m_browser.ExecuteScriptAsync(js);

            this.m_submitted = true;
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
            Task.Factory.StartNew(() => this.m_browser.Load("https://tweetdeck.twitter.com/"));
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
