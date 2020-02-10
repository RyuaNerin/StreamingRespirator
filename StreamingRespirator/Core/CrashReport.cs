using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sentry;

namespace StreamingRespirator.Core
{
    internal static class CrashReport
    {
        public static void Init()
        {
            SentrySdk.Init(opt =>
            {
                opt.Dsn = new Dsn("https://30aad3bdbf4c4c0da01be29f7c3b5b1b@sentry.ryuar.in/13");

#if DEBUG
                opt.Release = "Debug";
                opt.Debug = true;
#else
                opt.Release = Assembly.GetExecutingAssembly().GetName().Version.ToString();
#endif
            });

            AppDomain.CurrentDomain.UnhandledException += (s, e) => SentrySdk.CaptureException(e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, e) => SentrySdk.CaptureException(e.Exception);
            Application.ThreadException += (s, e) => SentrySdk.CaptureException(e.Exception);
        }
    }
}
