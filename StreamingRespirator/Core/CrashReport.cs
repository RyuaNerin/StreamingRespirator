using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sentry;
using Sentry.Extensibility;

namespace StreamingRespirator.Core
{
    internal sealed class CrashReport : IExceptionFilter
    {
        private CrashReport()
        {
        }

        public static void Init()
        {
            SentrySdk.Init(opt =>
            {
                opt.Dsn = new Dsn("https://30aad3bdbf4c4c0da01be29f7c3b5b1b@sentry.ryuar.in/13");

#if DEBUG
                opt.Release = "Debug";
                opt.Debug = true;
#else
                opt.Release = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
#endif
                opt.MaxRequestBodySize = RequestSize.Always;
                opt.IsEnvironmentUser = true;
                opt.SendDefaultPii = true;

                opt.HttpProxy = null;

                opt.SampleRate = null;

                opt.AddExceptionFilter(new CrashReport());
            });

            AppDomain.CurrentDomain.UnhandledException += (s, e) => SentrySdk.CaptureException(e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, e) => SentrySdk.CaptureException(e.Exception);
            Application.ThreadException += (s, e) => SentrySdk.CaptureException(e.Exception);
        }

        public bool Filter(Exception ex)
        {
            if (ex is AggregateException aex)
            {
                return aex.InnerExceptions.Any(e => this.Filter(e.InnerException));
            }
            else
            {
                if (ex.InnerException != null && this.Filter(ex.InnerException))
                    return true;

                switch (ex)
                {
                    case TaskCanceledException _:
                        return true;

                    case WebException webEx:
                        return webEx.Status != WebExceptionStatus.ProtocolError;
                }

                return false;
            }
        }
    }
}
