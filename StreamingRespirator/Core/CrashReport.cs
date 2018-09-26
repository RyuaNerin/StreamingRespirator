using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpRaven;

namespace StreamingRespirator.Core
{
    internal static class CrashReport
    {
        private static readonly RavenClient ravenClient = new RavenClient("https://674195b1bcee4a2f9d6286583a723f42@sentry.io/1273667")
        {
            Environment = "StreamingRespirator",
            Release = Assembly.GetExecutingAssembly().GetName().Version.ToString()
        };

        public static void Init()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            AppDomain.CurrentDomain.UnhandledException += (s, e) => Error(e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, e) => Error(e.Exception);            
            Application.ThreadException += (s, e) => Error(e.Exception);
        }

        public static void Error(Exception ex)
        {
            var ev = new SharpRaven.Data.SentryEvent(ex)
            {
                Level = SharpRaven.Data.ErrorLevel.Error
            };

            ev.Tags.Add("ARCH", Environment.Is64BitOperatingSystem ? "x64" : "x86");
            ev.Tags.Add("OS",   Environment.OSVersion.VersionString);
            ev.Tags.Add("NET",  Environment.Version.ToString());

            ravenClient.CaptureAsync(ev);
        }
    }
}
