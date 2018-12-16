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

using Timer = System.Threading.Timer;

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

        private readonly Timer m_autoRefresh;

        private NotifyIcon         m_notifyIcon;
        private ContextMenuStrip   m_contextMenuStrip;
        private ToolStripMenuItem  m_stripAbout;
        private ToolStripSeparator m_stripSep0;
        private ToolStripMenuItem  m_stripCredentials;
        private ToolStripMenuItem  m_stripAutoRefresh;
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
            Cef.GetGlobalCookieManager().SetStoragePath(Program.CookiePath, true, null);

            this.m_server = new RespiratorServer();

            this.m_chromeReqeustHandler = new ChromeRequestHandler();
            this.m_chromeReqeustHandler.TweetdeckAuthorized += this.Twitter_TweetdeckAuthorized;
            this.m_chromeReqeustHandler.TwitterApiRersponse += this.Twitter_TwitterApiRersponse;
            this.m_chromeReqeustHandler.ColumnsUpdated += this.Twitter_ColumnsUpdated;

            this.m_browser = new ChromiumWebBrowser("https://tweetdeck.twitter.com/", Program.DefaultBrowserSetting, null, false)
            {
                RequestHandler = this.m_chromeReqeustHandler,
                LifeSpanHandler = new LifeSpanHandler(),
            };
            this.m_browser.FrameLoadEnd += this.Browser_FrameLoadEnd;

            try
            {
                this.m_browser.CreateBrowser(IntPtr.Zero, Program.DefaultBrowserSetting);
            }
            catch
            {
                throw;
            }

            // MinimalRenderHandler 로 교체
            var old = this.m_browser.RenderHandler;
            this.m_browser.RenderHandler = new NullRenderHandler();
            old.Dispose();

#if DEBUG
            this.m_browser.AddressChanged     += (s, e) => Debug.WriteLine("AddressChanged : " + e.Address);
            this.m_browser.BrowserInitialized += (s, e) => Debug.WriteLine("BrowserInitialized");
            this.m_browser.ConsoleMessage     += (s, e) => Debug.WriteLine("ConsoleMessage : " + e.Message);
            this.m_browser.FrameLoadEnd       += (s, e) => Debug.WriteLine("FrameLoadEnd : " + e.Url);
            this.m_browser.FrameLoadStart     += (s, e) => Debug.WriteLine("FrameLoadStart : " + e.Url);
            this.m_browser.LoadingStateChanged+= (s, e) => Debug.WriteLine("LoadingStateChanged : " + e.IsLoading);
            this.m_browser.LoadError          += (s, e) => Debug.WriteLine("LoadError : " + e.ErrorText);
            this.m_browser.Paint              += (s, e) => Debug.WriteLine($"Paint : w:{e.Width} x h:{e.Height}");
            this.m_browser.StatusMessage      += (s, e) => Debug.WriteLine("StatusMessage : " + e.Value);
            this.m_browser.TitleChanged       += (s, e) => Debug.WriteLine("TitleChanged : " + e.Title);
#endif

            this.m_autoRefresh = new Timer(this.AutoRefresh);
        }

        private void InitializeComponent()
        {
            this.m_stripAbout = new ToolStripMenuItem("By RyuaNerin");
            this.m_stripAbout.Click += this.StripAbout_Click;

            this.m_stripSep0 = new ToolStripSeparator();

            this.m_stripCredentials = new ToolStripMenuItem("활성화된 아이디")
            {
                Enabled = false
            };
            (this.m_stripCredentials.DropDown as ToolStripDropDownMenu).ShowImageMargin = false;
            (this.m_stripCredentials.DropDown as ToolStripDropDownMenu).ImageScalingSize = new Size(32 * 3, 32);

            this.m_stripAutoRefresh = new ToolStripMenuItem("자동 새로고침")
            {
                CheckOnClick = true
            };
            this.m_stripAutoRefresh.CheckedChanged += this.StripAutoRefresh_CheckedChanged;

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
                Items =
                {
                    this.m_stripAbout,
                    this.m_stripSep0,
                    this.m_stripCredentials,
                    this.m_stripAutoRefresh,
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

        private void AutoRefresh(object state)
        {
            this.m_control.Invoke(new Action(this.m_browser.Reload));
        }

        private void StripAutoRefresh_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.m_server.IsRunning)
                return;

            this.SetAutoRefresh();
        }

        private void SetAutoRefresh()
        {
            if ((bool)this.m_control.Invoke(new Func<bool>(() => this.m_stripAutoRefresh.Checked)))
                this.m_autoRefresh.Change(TimeSpan.FromHours(3), TimeSpan.FromMilliseconds(-1));
            else
                this.m_autoRefresh.Change(0, 0);
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
                if (!await Task.Factory.StartNew(this.StartProxy))
                {
                    Application.Exit();
                    return;
                }

                this.m_notifyIcon.Text = $"스트리밍 호흡기";
                this.m_notifyIcon.Visible = true;
            }
            else
            {
                this.m_browser.Load("https://twitter.com/login?hide_message=true&redirect_after_login=https%3A%2F%2Ftweetdeck.twitter.com%2F%3Fvia_twitter_login%3Dtrue");
            }
        }

        private bool StartProxy()
        {
            try
            {
                this.m_server.Start();
                return true;
            }
            catch
            {
                MessageBox.Show("호흡기 작동중에 오류가 발생하였습니다.", "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
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

        private void Browser_FrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if (!e.Frame.IsMain)
                return;

            if (!Uri.TryCreate(e.Browser.MainFrame.Url, UriKind.Absolute, out var uri))
                return;

            if (uri.Host == "tweetdeck.twitter.com")
                this.SetAutoRefresh();

            if ((uri.Host == "twitter.com" || uri.Host == "www.twitter.com") &&
                (uri.AbsolutePath == "/login" || uri.AbsolutePath == "/login/error"))
            {
                Task.Factory.StartNew(new Action(this.StartLogin));
            }
        }

        private void StartLogin()
        {
            // 에러띄우기
            try
            {
                var task = this.m_browser.EvaluateScriptAsync("(function() { return document.getElementsByClassName('message-text')[0].innerText; })()", TimeSpan.FromSeconds(1));
                task.Wait();
                if (task.Result.Success)
                {
                    var err_msg = task.Result.Result as string;
                    if (!string.IsNullOrWhiteSpace(err_msg))
                        MessageBox.Show(err_msg, "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            catch
            { }

            // 아이디 기본값
            string defaultUsername = null;
            try
            {
                var task = this.m_browser.EvaluateScriptAsync("(function() { return document.getElementsByClassName('js-username-field')[0].value; })()", TimeSpan.FromSeconds(1));
                task.Wait();
                if (task.Result.Success)
                    defaultUsername = task.Result.Result as string;
            }
            catch
            { }

            string username = null, password = null;
            if ((bool)this.m_control.Invoke(new Func<bool>(
                () =>
                {
                    using (var frm = new LoginWindow(defaultUsername))
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
                    form.getElementsByClassName('js-username-field')[0].value = '{username.Replace("\\", "\\\\").Replace("'", "\\'")}';
                    form.getElementsByClassName('js-password-field')[0].value = '{password.Replace("\\", "\\\\").Replace("'", "\\'")}';
                    setTimeout(function() {{ form.submit(); }}, 1000);
                }})();";

            this.m_browser.ExecuteScriptAsync(js);
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
            //Task.Factory.StartNew(this.RecreateBrowser);
            //this.m_control.BeginInvoke(new Action(this.RecreateBrowser));
            this.m_control.BeginInvoke(new Action(() =>
            {
                this.m_browser.Reload(true);
            }));
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
                    b |= Hook.HookWinInet(RespiratorServer.ProxyPort, process);

            if (b)
            {
                MessageBox.Show("아즈레아에 스트리밍 호흡기를 적용하였습니다.", "스트리밍 호흡기", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
