using System;
using System.IO;
using System.Windows.Forms;
using CefSharp;

namespace StreamingRespirator
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            CefSharpSettings.ShutdownOnExit = true;
            CefSharpSettings.SubprocessExitIfParentProcessClosed = true;
            CefSharpSettings.WcfEnabled = false;
            //CefSharpSettings.Proxy = null;

            var cefSettings = new CefSettings
            {
                CachePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), ".cache")
            };
            cefSettings.DisableTouchpadAndWheelScrollLatching();
            cefSettings.DisableGpuAcceleration();

            Cef.Initialize(cefSettings: cefSettings,
                           performDependencyCheck: true,
                           browserProcessHandler: null);
            Cef.EnableHighDPISupport();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            Cef.Shutdown();
        }
    }
}
