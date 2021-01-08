using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Sentry;
using StreamingRespirator.Core.Streaming;
using StreamingRespirator.Core.Windows;

namespace StreamingRespirator.Core
{
    internal class MainContext : ApplicationContext
    {
        private readonly Control m_invoker;

        private NotifyIcon m_notifyIcon;
        private ContextMenuStrip m_contextMenuStrip;
        private ToolStripMenuItem m_stripAbout;
        private ToolStripLabel m_scripPort;
        private ToolStripMenuItem m_stripConfig;
        private ToolStripSeparator m_stripSepAccount;
        private ToolStripMenuItem m_stripAdd;
        private ToolStripSeparator m_stripSepExit;
        private ToolStripMenuItem m_stripExit;

        private RespiratorServer m_server;

        public MainContext()
        {
            this.m_invoker = new Control();
            this.m_invoker.CreateControl();

            TwitterClientFactory.ClientUpdated += this.TwitterClientFactory_ClientUpdated;

            this.InitializeComponent();
            Lang.ApplyLang(this);

            if (TwitterClientFactory.AccountCount == 0)
                this.m_notifyIcon.ShowBalloonTip(10000, Lang.Name, Lang.MainContext__NoAccount, ToolTipIcon.Info);
            else
            {
                foreach (var user in Config.Instance.Accounts)
                {
                    this.m_clients.Add(user.Id, this.NewClientToolStripItems(user.Id, user.ScreenName));
                }
            }

            Program.CheckUpdate(this);

            this.StartServer();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    this.m_server.Dispose();
                }
                catch
                {
                }
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.m_stripAbout = new ToolStripMenuItem("By RyuaNerin");
            this.m_stripAbout.Click += this.StripAbout_Click;

            ////////////////////////////////////////////////////////////

            this.m_stripConfig = new ToolStripMenuItem("호흡기 설정");
            this.m_stripConfig.Click += this.StripConfig_Click;

            ////////////////////////////////////////////////////////////

            this.m_scripPort = new ToolStripLabel("포트 : ");

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
                    this.m_scripPort,
                    this.m_stripConfig,
                    this.m_stripSepAccount,
                    this.m_stripAdd,
                    this.m_stripSepExit,
                    this.m_stripExit,
                },
            };

            this.m_notifyIcon = new NotifyIcon
            {
                Icon = Properties.Resources.icon,
                ContextMenuStrip = this.m_contextMenuStrip,
                Visible = true,
            };
            this.m_notifyIcon.BalloonTipClicked += this.NotifyIcon_BalloonTipClicked;
        }

        public void StartServer()
        {
            var port = Config.Instance.Proxy.Port;
            if (this.m_server?.Port == port)
                return;

            try
            {
                this.m_invoker.Invoke(new Action(() =>
                {
                    try
                    {
                        this.m_scripPort.ForeColor = Color.Red;
                        this.m_scripPort.Text = string.Format(Lang.MainContext__m_scripPort__Text, "");
                    }
                    catch
                    {
                    }
                }));
            }
            catch
            {
            }

            try
            {
                this.m_server?.Dispose();
                this.m_server = null;
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }

            try
            {
                this.m_server = new RespiratorServer(port);

                this.m_invoker.Invoke(new Action(() =>
                {
                    try
                    {
                        this.m_scripPort.ForeColor = SystemColors.ControlText;
                        this.m_scripPort.Text = string.Format(Lang.MainContext__m_scripPort__Text, port);
                    }
                    catch
                    {
                    }
                }));
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);

                this.m_invoker.Invoke(new Action(() => MessageBox.Show(Lang.StartError, Lang.Name, MessageBoxButtons.OK, MessageBoxIcon.Information)));
            }
        }

        protected override void ExitThreadCore()
        {
            this.m_notifyIcon.Visible = false;

            base.ExitThreadCore();
        }

        private void StripConfig_Click(object sender, EventArgs e)
        {
            using (var frm = new ConfigWindow())
            {
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    this.StartServer();
                }
            }
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

        private ClientToolStripItems NewClientToolStripItems(long id, string screenName)
        {
            var st = new ClientToolStripItems
            {
                Remove = new ToolStripMenuItem(Lang.MainContext__Client__Remove)
                {
                    Tag = id,
                },
                RootItem = new ToolStripMenuItem(screenName)
                {
                    Checked = false,
                    CheckOnClick = false,

                },
                TlHome = new ToolStripMenuItem("-")
                {
                    Image = Properties.Resources.uniF053,
                    Tag = id,
                },
                TlAbountMe = new ToolStripMenuItem("-")
                {
                    Image = Properties.Resources.uniF055,
                    Tag = id,
                },
                TlDm = new ToolStripMenuItem("-")
                {
                    Image = Properties.Resources.uniF054,
                    Tag = id,
                },
            };

            st.Remove.Click += new EventHandler(this.StripRemoveClient_Click);

            var refreshButton = new ToolStripMenuItem(Lang.MainContext__Client__Refresh)
            {
                Tag = id,
            };
            refreshButton.Click += new EventHandler((s, e) => TwitterClientFactory.GetInsatnce((long)(s as ToolStripMenuItem).Tag)?.ForceRefresh(true, true, true));
            st.TlHome.Click += new EventHandler((s, e) => TwitterClientFactory.GetInsatnce((long)(s as ToolStripMenuItem).Tag)?.ForceRefresh(true, false, false));
            st.TlAbountMe.Click += new EventHandler((s, e) => TwitterClientFactory.GetInsatnce((long)(s as ToolStripMenuItem).Tag)?.ForceRefresh(false, true, false));
            st.TlDm.Click += new EventHandler((s, e) => TwitterClientFactory.GetInsatnce((long)(s as ToolStripMenuItem).Tag)?.ForceRefresh(false, false, true));

            st.RootItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                st.Remove,
                new ToolStripSeparator(),
                refreshButton,
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
                            this.m_clients.Add(id, this.NewClientToolStripItems(id, data.ScreenName));

                        var cts = this.m_clients[id];

                        if (data.IsRemoved)
                        {
                            this.m_contextMenuStrip.Items.Remove(cts.RootItem);

                            foreach (var subItem in cts.RootItem.DropDownItems.OfType<ToolStripMenuItem>().ToArray())
                                subItem.Dispose();
                            cts.RootItem.Dispose();

                            this.m_clients.Remove(id);
                        }
                        else
                        {
                            if (data.ScreenName != null) cts.RootItem.Text = data.ScreenName;
                            if (data.Connected.HasValue)
                            {
                                cts.RootItem.Checked = data.Connected.Value;

                                if (!data.Connected.Value)
                                {
                                    cts.TlHome.Text = "-";
                                    cts.TlAbountMe.Text = "-";
                                    cts.TlDm.Text = "-";
                                }
                            }

                            if (data.WaitTimeHome.HasValue) cts.TlHome.Text = FormatWaitTime(data.WaitTimeHome.Value);
                            if (data.WaitTimeAboutMe.HasValue) cts.TlAbountMe.Text = FormatWaitTime(data.WaitTimeAboutMe.Value);
                            if (data.WaitTimeDm.HasValue) cts.TlDm.Text = FormatWaitTime(data.WaitTimeDm.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }
        }

        private static string FormatWaitTime(TimeSpan waitTime)
        {
            return string.Format("{0:HH:mm:ss} ({1:##0.0}s)", DateTime.Now.Add(waitTime), waitTime.TotalSeconds);
        }

        private void StripRemoveClient_Click(object sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            TwitterClientFactory.RemoveClient((long)item.Tag);
        }

        private void NotifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            TwitterClientFactory.AddClient();
        }
        private void StripAdd_Click(object sender, EventArgs e)
        {
            TwitterClientFactory.AddClient();
        }

        private void StripAbout_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start("https://ryuanerin.kr")?.Dispose();
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }
        }

        private void StripExit_Click(object sender, EventArgs e)
        {
            this.m_notifyIcon.Visible = false;
            this.ExitThreadCore();
        }
    }
}
