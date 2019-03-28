using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using StreamingRespirator.Core.Streaming;

namespace StreamingRespirator.Core
{
    internal class MainContext : ApplicationContext
    {
        private readonly Control m_invoker;

        private readonly RespiratorServer m_server;

        private NotifyIcon         m_notifyIcon;
        private ContextMenuStrip   m_contextMenuStrip;
        private ToolStripMenuItem  m_stripAbout;
        private ToolStripSeparator m_stripSepConfig;
        private ToolStripMenuItem  m_startWithWindows;
        private ToolStripSeparator m_stripSepConfig2;
        private ToolStripMenuItem  m_stripRetweet;
        private ToolStripMenuItem  m_stripMyRetweet;
        private ToolStripSeparator m_stripSepAccount;
        private ToolStripMenuItem  m_stripAdd;
        private ToolStripSeparator m_stripSepExit;
        private ToolStripMenuItem  m_stripExit;

        public MainContext()
        {
            this.m_invoker = new Control();
            this.m_invoker.CreateControl();

            this.m_server = new RespiratorServer();

            TwitterClientFactory.ClientUpdated += this.TwitterClientFactory_ClientUpdated;

            this.InitializeComponent();

            Config.Load();
            this.m_startWithWindows.Checked = Config.StartWithWindows;

            if (!this.StartProxy())
                Application.Exit();

            if (TwitterClientFactory.AccountCount == 0)
                this.m_notifyIcon.ShowBalloonTip(10000, "스트리밍 호흡기", "계정이 추가되어있지 않습니다!\n\n계정을 추가해주세요!", ToolTipIcon.Info);
        }

        private void InitializeComponent()
        {
            this.m_stripAbout = new ToolStripMenuItem("By RyuaNerin");
            this.m_stripAbout.Click += this.StripAbout_Click;

            ////////////////////////////////////////////////////////////
            
            this.m_stripSepConfig = new ToolStripSeparator();

            this.m_startWithWindows = new ToolStripMenuItem("윈도우 시작시 자동 실행")
            {
                Checked = Config.StartWithWindows,
            };
            this.m_startWithWindows.Click += this.StartWithWindows_Click;

            ////////////////////////////////////////////////////////////

            this.m_stripSepConfig2 = new ToolStripSeparator();

            this.m_stripRetweet = new ToolStripMenuItem("리트윗된 내 트윗 표시")
            {
                CheckOnClick = true,
                Checked = Config.Filter.ShowRetweetedMyStatus,
            };
            this.m_stripRetweet.CheckedChanged += (s, e) => Config.Filter.ShowRetweetedMyStatus = this.m_stripRetweet.Checked;

            this.m_stripMyRetweet = new ToolStripMenuItem("내 리트윗 다시 표시")
            {
                CheckOnClick = true,
                Checked      = Config.Filter.ShowMyRetweet,
            };
            this.m_stripMyRetweet.CheckedChanged += (s, e) => Config.Filter.ShowMyRetweet = this.m_stripMyRetweet.Checked;

            ////////////////////////////////////////////////////////////

            this.m_stripSepAccount = new ToolStripSeparator();

            this.m_stripAdd = new ToolStripMenuItem("계정 추가");
            this.m_stripAdd.Click += this.StripAdd_Click;
            this.m_stripSepAccount = new ToolStripSeparator();

            ////////////////////////////////////////////////////////////

            this.m_stripSepExit = new ToolStripSeparator();

            this.m_stripExit = new ToolStripMenuItem("종료");
            this.m_stripExit.Click += this.StripExit_Click;

            this.m_contextMenuStrip = new ContextMenuStrip
            {
                //RenderMode      = ToolStripRenderMode.Professional,
                Items =
                {
                    this.m_stripAbout,
                    this.m_stripSepConfig,
                    this.m_startWithWindows,
                    this.m_stripSepConfig2,
                    this.m_stripRetweet,
                    this.m_stripMyRetweet,
                    this.m_stripSepAccount,
                    this.m_stripAdd,
                    this.m_stripSepExit,
                    this.m_stripExit,
                },
            };

            this.m_notifyIcon = new NotifyIcon
            {
                Icon             = Properties.Resources.icon,
                ContextMenuStrip = this.m_contextMenuStrip,
                Visible          = true,
            };
            this.m_notifyIcon.BalloonTipClicked += this.NotifyIcon_BalloonTipClicked;
        }

        private struct ClientToolStripItems
        {
            public ToolStripMenuItem RootItem;

            public ToolStripItem Remove;

            public ToolStripItem TlHome;
            public ToolStripItem TlAbountMe;
            public ToolStripItem TlDm;
        }
        private readonly Dictionary<long, ClientToolStripItems> m_clients = new Dictionary<long, ClientToolStripItems>();

        private ClientToolStripItems NewClientToolStripItems(long id, StateUpdateData data)
        {
            var st = new ClientToolStripItems
            {
                Remove = new ToolStripMenuItem("삭제")
                {
                    Tag = id,
                },
                RootItem = new ToolStripMenuItem(data.ScreenName)
                {
                    Checked = false,
                    CheckOnClick = false,

                },
                TlHome = new ToolStripMenuItem("-")
                {
                    Image = Properties.Resources.uniF053,
                },
                TlAbountMe = new ToolStripMenuItem("-")
                {
                    Image = Properties.Resources.uniF055,
                },
                TlDm = new ToolStripMenuItem("-")
                {
                    Image = Properties.Resources.uniF054,
                },
            };

            st.Remove.Click += new EventHandler(this.StripRemoveClient_Click);

            st.RootItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                st.Remove,
                new ToolStripSeparator(),
                st.TlHome,
                st.TlAbountMe,
                st.TlDm,
            });

            this.m_contextMenuStrip.Items.Insert(this.m_contextMenuStrip.Items.IndexOf(this.m_stripSepExit), st.RootItem);

            return st;
        }
        private void TwitterClientFactory_ClientUpdated(long id, StateUpdateData data)
        {
            try
            {
                if (this.m_invoker.InvokeRequired)
                {
                    this.m_invoker.Invoke(new Action<long, StateUpdateData>(this.TwitterClientFactory_ClientUpdated), id, data);
                }
                else
                {
                    lock (this.m_clients)
                    {
                        if (!data.IsRemoved && !this.m_clients.ContainsKey(id))
                            this.m_clients.Add(id, this.NewClientToolStripItems(id, data));

                        var cts = this.m_clients[id];

                        if (data.IsRemoved)
                        {
                            foreach (Control subItem in cts.RootItem.DropDownItems)
                                subItem.Dispose();
                            cts.RootItem.Dispose();

                            this.m_clients.Remove(id);
                        }
                        else
                        {
                            var now = DateTime.Now;

                            if (data.ScreenName != null) cts.RootItem.Text = data.ScreenName;
                            if (data.Connected.HasValue)
                            {
                                cts.RootItem.Checked = data.Connected.Value;

                                if (!data.Connected.Value)
                                {
                                    cts.TlHome    .Text = "-";
                                    cts.TlAbountMe.Text = "-";
                                    cts.TlDm      .Text = "-";
                                }
                            }

                            if (data.WaitTimeHome   .HasValue) cts.TlHome    .Text    = FormatWaitTime(data.WaitTimeHome   .Value);
                            if (data.WaitTimeAboutMe.HasValue) cts.TlAbountMe.Text    = FormatWaitTime(data.WaitTimeAboutMe.Value);
                            if (data.WaitTimeDm     .HasValue) cts.TlDm      .Text    = FormatWaitTime(data.WaitTimeDm     .Value);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static string FormatWaitTime(double waitTime)
        {
            var now = DateTime.Now;
            return string.Format("{0:HH:mm:ss} ({1:##0.0}s)", now.AddSeconds(waitTime), waitTime);
        }

        private volatile ManualResetEventSlim m_startWithWindowsWorking = new ManualResetEventSlim(false);
        private void StartWithWindows_Click(object sender, EventArgs e)
        {
            if (this.m_startWithWindowsWorking.IsSet)
                return;

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

                    Config.StartWithWindows = true;
                    this.m_startWithWindows.Checked = true;
                    Config.Save();
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

                Config.StartWithWindows = false;
                this.m_startWithWindows.Checked = false;
                Config.Save();
            }

            this.m_startWithWindowsWorking.Reset();
        }

        private void StripRemoveClient_Click(object sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            TwitterClientFactory.RemoveClient((long)item.Tag);
        }

        public void StopProxy()
        {
            this.m_server.Stop();

            this.m_notifyIcon.Visible = false;
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


        private void NotifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            Task.Factory.StartNew(() => TwitterClientFactory.AddClient(this.m_invoker));
        }
        private void StripAdd_Click(object sender, EventArgs e)
        {
            Task.Factory.StartNew(() => TwitterClientFactory.AddClient(this.m_invoker));
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
        
        private void StripExit_Click(object sender, EventArgs e)
        {
            //Application.Exit();
            this.ExitThreadCore();
        }
    }
}
