using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Sentry;
using StreamingRespirator.Core.Streaming;

namespace StreamingRespirator.Core
{
    internal static class Program
    {
        public const string MutexName = "{5FF75362-95BA-4399-8C77-C1A0C5B8A291}";

        public static readonly string ConfigPath = Path.ChangeExtension(Application.ExecutablePath, ".cnf");

        public static readonly JsonSerializer JsonSerializer = new JsonSerializer
        {
            DateFormatString = "ddd MMM dd HH:mm:ss +ffff yyyy",
            Formatting = Formatting.None,
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
        };

        [STAThread]
        static void Main()
        {
            using (var mut = new Mutex(true, MutexName, out bool createdNew))
            {
                if (!createdNew)
                    return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                WebRequest.DefaultCachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
#if !DEBUG
                System.Net.WebRequest.DefaultWebProxy = null;
#endif

                CrashReport.Init();

                if (Assembly.GetExecutingAssembly().GetName().Version.ToString() != "0.0.0.0" && !CheckUpdate())
                {
                    return;
                }

                if (!Certificates.InstallCACertificates())
                {
                    MessageBox.Show(Lang.CertificateError, Lang.Name, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                RespiratorServer server;
                try
                {
                    server = new RespiratorServer();
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureException(ex);

                    MessageBox.Show(Lang.StartError, Lang.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (server)
                {
                    Application.Run(new MainContext(server));
                    Config.Save();
                }
            }
        }

        static bool CheckUpdate()
        {
            if (GithubLatestRelease.CheckNewVersion())
            {
                MessageBox.Show(Lang.NewUpdate, Lang.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);

                try
                {
                    Process.Start("https://github.com/RyuaNerin/StreamingRespirator/blob/master/README.md")?.Dispose();
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureException(ex);
                }

                return false;
            }

            return true;
        }
    }
}
