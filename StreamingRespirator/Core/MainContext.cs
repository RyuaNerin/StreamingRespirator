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
        private ToolStripLabel     m_stripConnected;
        private ToolStripSeparator m_stripSep1;
        private ToolStripMenuItem  m_stripExit;

        private readonly Dictionary<long, ToolStripLabel> m_connected = new Dictionary<long, ToolStripLabel>();

        public MainContext()
        {
            this.m_invoker = new Control();
            this.m_invoker.CreateControl();

            this.m_server = new RespiratorServer(this.m_invoker);
            this.m_server.NewConnection  += this.Server_NewConnection;
            this.m_server.LostConnection += this.Server_LostConnection;

            this.InitializeComponent();

            if (!this.StartProxy())
                Application.Exit();
        }

        private void InitializeComponent()
        {
            this.m_stripAbout = new ToolStripMenuItem("By RyuaNerin");
            this.m_stripAbout.Click += this.StripAbout_Click;

            this.m_stripSep0 = new ToolStripSeparator();

            this.m_stripConnected = new ToolStripLabel("연결된 스트리밍");

            this.m_stripSep1 = new ToolStripSeparator();

            this.m_stripExit = new ToolStripMenuItem("종료");
            this.m_stripExit.Click += this.StripExit_Click;

            this.m_contextMenuStrip = new ContextMenuStrip
            {
                RenderMode      = ToolStripRenderMode.Professional,
                Items =
                {
                    this.m_stripAbout,
                    this.m_stripSep0,
                    this.m_stripConnected,
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

        private void Server_NewConnection(long id, string screenName)
        {
            if (this.m_invoker.InvokeRequired)
            {
                this.m_invoker.Invoke(new Action<long, string>(this.Server_NewConnection), id, screenName);
            }
            else
            {
                lock (this.m_connected)
                {
                    if (this.m_connected.ContainsKey(id))
                        return;

                    var item = new ToolStripLabel(screenName);
                    this.m_connected.Add(id, item);

                    this.m_contextMenuStrip.Items.Insert(this.m_contextMenuStrip.Items.Count - 2, item);
                }
            }
        }

        private void Server_LostConnection(long id)
        {
            if (this.m_invoker.InvokeRequired)
            {
                this.m_invoker.Invoke(new Action<long>(this.Server_LostConnection), id);
            }
            else
            {
                lock (this.m_connected)
                {
                    if (!this.m_connected.ContainsKey(id))
                        return;

                    var item = this.m_connected[id];
                    this.m_contextMenuStrip.Items.Remove(item);
                    this.m_connected.Remove(id);
                }
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
