using System;
using System.Diagnostics;
using System.Windows.Forms;
using StreamingRespirator.Core.Streaming;

namespace StreamingRespirator.Core
{
    internal class MainContext : ApplicationContext
    {
        private readonly RespiratorServer m_server = new RespiratorServer();

        public static Control Invoker { get; private set; }
        private readonly Control m_control;

        private NotifyIcon         m_notifyIcon;
        private ContextMenuStrip   m_contextMenuStrip;
        private ToolStripMenuItem  m_stripAbout;
        private ToolStripSeparator m_stripSep0;
        private ToolStripMenuItem  m_stripExit;

        public MainContext()
        {
            this.m_control = new Control();
            this.m_control.CreateControl();

            Invoker = this.m_control;

            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.m_stripAbout = new ToolStripMenuItem("By RyuaNerin");
            this.m_stripAbout.Click += this.StripAbout_Click;

            this.m_stripSep0 = new ToolStripSeparator();

            this.m_stripExit = new ToolStripMenuItem("종료");
            this.m_stripExit.Click += this.StripExit_Click;

            this.m_contextMenuStrip = new ContextMenuStrip
            {
                RenderMode      = ToolStripRenderMode.Professional,
                Items =
                {
                    this.m_stripAbout,
                    this.m_stripSep0,
                    this.m_stripExit,
                },
            };

            this.m_notifyIcon = new NotifyIcon
            {
                Icon             = Properties.Resources.tray,
                ContextMenuStrip = this.m_contextMenuStrip,
                Visible          = true,
            };

            if (!this.StartProxy())
            {
                Application.Exit();
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
