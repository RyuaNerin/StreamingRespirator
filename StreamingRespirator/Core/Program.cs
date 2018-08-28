using System;
using System.IO;
using System.Windows.Forms;
using CefSharp;

namespace StreamingRespirator.Core
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            CefSharpSettings.SubprocessExitIfParentProcessClosed = true;
            CefSharpSettings.ShutdownOnExit = true;
            CefSharpSettings.WcfEnabled = false;
            CefSharpSettings.Proxy = null;

            var cefSettings = new CefSettings
            {
                CachePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), ".cache"),
                LogSeverity = LogSeverity.Disable,
                LogFile = null,
                WindowlessRenderingEnabled = true,
            };

            cefSettings.CefCommandLineArgs.Add("no-proxy-server", "1");
            cefSettings.CefCommandLineArgs.Add("disable-extensions", "1");
            cefSettings.CefCommandLineArgs.Add("mute-audio", "1");
            cefSettings.CefCommandLineArgs.Add("disable-features", "AsyncWheelEvents,TouchpadAndWheelScrollLatching");
            cefSettings.CefCommandLineArgs.Add("disable-gpu-vsync", "1");
            cefSettings.CefCommandLineArgs.Add("disable-gpu", "1");

            cefSettings.DisableGpuAcceleration();
            cefSettings.SetOffScreenRenderingBestPerformanceArgs();

            Cef.Initialize(cefSettings, true, null);
            Cef.EnableHighDPISupport();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Windows.MainWindow());

            Cef.Shutdown();
        }
    }
}
