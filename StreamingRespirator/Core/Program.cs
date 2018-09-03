using System;
using System.IO;
using System.Windows.Forms;
using CefSharp;

namespace StreamingRespirator.Core
{
    internal static class Program
    {
        public static CefSettings     DefaultCefSetting { get; }
        public static BrowserSettings DefaultBrowserSetting { get; }

        public static readonly string CookiePath;

        static Program()
        {
            DefaultCefSetting = new CefSettings
            {
                CachePath                  = null,
                LogSeverity                = LogSeverity.Disable,
                LogFile                    = null,
                WindowlessRenderingEnabled = true,
                CefCommandLineArgs =
                {
                    { "no-proxy-server"          , "1" },
                    { "mute-audio"               , "1" },
                    { "disable-application-cache", "1" },
                    { "disable-extensions"       , "1" },
                    { "disable-features"         , "AsyncWheelEvents,TouchpadAndWheelScrollLatching" },
                    { "disable-gpu"              , "1" },
                    { "disable-gpu-vsync"        , "1" },
                    { "disable-gpu-compositing"  , "1" },
                }
            };
            DefaultCefSetting.DisableGpuAcceleration();
            DefaultCefSetting.SetOffScreenRenderingBestPerformanceArgs();

            DefaultBrowserSetting = new BrowserSettings
            {
                DefaultEncoding           = "UTF-8",
                WebGl                     = CefState.Disabled,
                Plugins                   = CefState.Disabled,
                JavascriptAccessClipboard = CefState.Disabled,
                ImageLoading              = CefState.Disabled,
                JavascriptCloseWindows    = CefState.Disabled,
                ApplicationCache          = CefState.Disabled,
                RemoteFonts               = CefState.Disabled,
                WindowlessFrameRate       = 1,
                //LocalStorage              = CefState.Disabled, // => Uncaught TypeError: Cannot read property 'getItem' of null
                Databases                 = CefState.Disabled,
            };

            CookiePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), ".cookie");
        }

        [STAThread]
        static void Main()
        {
            CefSharpSettings.SubprocessExitIfParentProcessClosed = true;
            CefSharpSettings.ShutdownOnExit = true;
            CefSharpSettings.WcfEnabled = false;
            CefSharpSettings.Proxy = null;

            Cef.Initialize(DefaultCefSetting, false, null);
            Cef.EnableHighDPISupport();

            Cef.GetGlobalCookieManager().SetStoragePath(CookiePath, true, null);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);            
            Application.Run(new Windows.MainWindow());

            Cef.Shutdown();
        }
    }
}
