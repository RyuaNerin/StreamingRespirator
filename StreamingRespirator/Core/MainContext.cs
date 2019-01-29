using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private ToolStripSeparator m_stripSep0;
        private ToolStripMenuItem  m_stripAdd;
        private ToolStripSeparator m_stripSep1;
        private ToolStripMenuItem  m_stripExit;

        private readonly Dictionary<long, ToolStripMenuItem> m_clients = new Dictionary<long, ToolStripMenuItem>();

        public MainContext()
        {
            this.m_invoker = new Control();
            this.m_invoker.CreateControl();

            this.m_server = new RespiratorServer();

            TwitterClientFactory.ClientAdded   += this.TwitterClientFactory_ClientAdded;
            TwitterClientFactory.ClientUpdated += this.TwitterClientFactory_ClientUpdated;
            TwitterClientFactory.ClientRemoved += this.TwitterClientFactory_ClientRemoved;

            TwitterClientFactory.ClientStarted += this.TwitterClientFactory_ClientStarted;
            TwitterClientFactory.ClientStoped  += this.TwitterClientFactory_ClientStoped;

            this.InitializeComponent();

            TwitterClientFactory.LoadCookie();

            if (!this.StartProxy())
                Application.Exit();
        }

        private void InitializeComponent()
        {
            this.m_stripAbout = new ToolStripMenuItem("By RyuaNerin");
            this.m_stripAbout.Click += this.StripAbout_Click;

            this.m_stripSep0 = new ToolStripSeparator();

            this.m_stripAdd = new ToolStripMenuItem("계정 추가");
            this.m_stripAdd.Click += this.StripAdd_Click;

            this.m_stripSep1 = new ToolStripSeparator();

            this.m_stripExit = new ToolStripMenuItem("종료");
            this.m_stripExit.Click += this.StripExit_Click;

            this.m_contextMenuStrip = new ContextMenuStrip
            {
                //RenderMode      = ToolStripRenderMode.Professional,
                Items =
                {
                    this.m_stripAbout,
                    this.m_stripSep0,
                    this.m_stripAdd,
                    this.m_stripSep1,
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

        private void TwitterClientFactory_ClientAdded(long id, string screenName)
        {
            try
            {
                if (this.m_invoker.InvokeRequired)
                {
                    this.m_invoker.Invoke(new Action<long, string>(this.TwitterClientFactory_ClientAdded), id, screenName);
                }
                else
                {
                    lock (this.m_clients)
                    {
                        if (this.m_clients.ContainsKey(id))
                            return;


                        var itemRemove = new ToolStripMenuItem("삭제")
                        {
                            Tag = id,
                        };
                        itemRemove.Click += new EventHandler(this.StripRemoveClient_Click);

                        var item = new ToolStripMenuItem(screenName)
                        {
                            Checked      = false,
                            CheckOnClick = false,
                        };
                        item.DropDownItems.Add(itemRemove);

                        this.m_clients.Add(id, item);
                        this.m_contextMenuStrip.Items.Insert(this.m_contextMenuStrip.Items.Count - 2, item);
                    }
                }
            }
            catch
            {
            }
        }

        private void StripRemoveClient_Click(object sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            TwitterClientFactory.RemoveClient((long)item.Tag);
        }

        private void TwitterClientFactory_ClientUpdated(long id, string screenName)
        {
            try
            {
                if (this.m_invoker.InvokeRequired)
                {
                    this.m_invoker.Invoke(new Action<long, string>(this.TwitterClientFactory_ClientUpdated), id, screenName);
                }
                else
                {
                    lock (this.m_clients)
                    {
                        this.m_clients[id].Text = screenName;
                    }
                }
            }
            catch
            {
            }
        }

        private void TwitterClientFactory_ClientRemoved(long id)
        {
            try
            {
                if (this.m_invoker.InvokeRequired)
                {
                    this.m_invoker.Invoke(new Action<long>(this.TwitterClientFactory_ClientRemoved), id);
                }
                else
                {
                    lock (this.m_clients)
                    {
                        var item = this.m_clients[id];

                        foreach (Control subItem in item.DropDownItems)
                            subItem.Dispose();

                        item.Dispose();

                        this.m_clients.Remove(id);
                    }
                }
            }
            catch
            {
            }
        }

        private void TwitterClientFactory_ClientStarted(long id)
        {
            try
            {
                if (this.m_invoker.InvokeRequired)
                {
                    this.m_invoker.Invoke(new Action<long>(this.TwitterClientFactory_ClientStarted), id);
                }
                else
                {
                    lock (this.m_clients)
                        this.m_clients[id].Checked = true;
                }
            }
            catch
            {
            }
        }
        private void TwitterClientFactory_ClientStoped(long id)
        {
            try
            {
                if (this.m_invoker.InvokeRequired)
                {
                    this.m_invoker.Invoke(new Action<long>(this.TwitterClientFactory_ClientStoped), id);
                }
                else
                {
                    lock (this.m_clients)
                        this.m_clients[id].Checked = false;
                }
            }
            catch
            {
            }
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

        private void StripAdd_Click(object sender, EventArgs e)
        {
            TwitterClientFactory.AddClient(this.m_invoker);
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
            Application.Exit();
        }
    }
}
