using System;
using System.Windows.Forms;
using CefSharp;

namespace StreamingRespirator
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            CefSharpSettings.ShutdownOnExit = false;
            CefSharpSettings.SubprocessExitIfParentProcessClosed = true;
            CefSharpSettings.WcfEnabled = false;
            //CefSharpSettings.Proxy = null;

            Cef.EnableHighDPISupport();
            Cef.Initialize(new CefSettings(), performDependencyCheck: true, browserProcessHandler: null);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            Cef.Shutdown();
        }
    }
}
