using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using CefSharp;
using CefSharp.OffScreen;

namespace StreamingRespirator.Core
{
    internal static class Program
    {
        public static readonly CefSettings     DefaultCefSetting;
        public static readonly BrowserSettings DefaultBrowserSetting;

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
                    { "disable-application-cache", "1" },
                    { "disable-extensions"       , "1" },
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!CheckUpdate())
                return;

            Application.Run(new MainContext());

            Cef.Shutdown();
        }

        static bool CheckUpdate()
        {
            if (GithubLatestRelease.CheckNewVersion())
            {
                MessageBox.Show("새로운 업데이트가 있습니다.", "스트리밍 호흡기");

                try
                {
                    Process.Start("https://github.com/RyuaNerin/StreamingRespirator/blob/master/README.md")?.Dispose();
                }
                catch
                {
                }

                return false;
            }

            return true;
        }
    }
}
