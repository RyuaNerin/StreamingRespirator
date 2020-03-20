using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Onova;
using Onova.Services;
using StreamingRespirator.Core.Streaming;

namespace StreamingRespirator.Core
{
    internal static class Program
    {
#if DEBUG
        public const string MutexName = "{5FF75362-95BA-4399-8C77-C1A0C5B8A292}";
#else
        public const string MutexName = "{5FF75362-95BA-4399-8C77-C1A0C5B8A291}";
#endif

        public static readonly string ConfigPath = Path.ChangeExtension(Application.ExecutablePath, ".cnf");

        public static readonly JsonSerializer JsonSerializer = new JsonSerializer
        {
            DateFormatString = "ddd MMM dd HH:mm:ss +ffff yyyy",
            Formatting = Formatting.None,
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
        };

        public static readonly ManualResetEvent NetworkAvailable = new ManualResetEvent(false);

        static Program()
        {
            NetworkChange.NetworkAvailabilityChanged += (s, e) =>
            {
                if (e.IsAvailable)
                    NetworkAvailable.Set();
                else
                    NetworkAvailable.Reset();
            };

            if (NetworkInterface.GetIsNetworkAvailable())
                NetworkAvailable.Set();
        }

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

                if (!Certificates.InstallCACertificates())
                {
                    MessageBox.Show(Lang.CertificateError, Lang.Name, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                Config.Load();
                using (var context = new MainContext())
                {
                    Application.Run(context);
                    Config.Save();
                }
            }
        }

        public static async void CheckUpdate(ApplicationContext context)
        {
            if (Assembly.GetExecutingAssembly().GetName().Version.ToString() != "0.0.0.0")
            {
                using (var manager = new UpdateManager(new GithubPackageResolver("RyuaNerin", "StreamingRespirator", "*.exe"), new ExecutablePackageExtractor()))
                {
                    var r = await manager.CheckForUpdatesAsync();

                    if (r.CanUpdate)
                    {
                        await manager.PrepareUpdateAsync(r.LastVersion);

                        manager.LaunchUpdater(r.LastVersion, true);

                        if (MessageBox.Show(Lang.NewUpdate, Lang.Name, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.Cancel)
                            return;

                        context.ExitThread();
                    }
                }
            }
        }

        private class ExecutablePackageExtractor : IPackageExtractor
        {
            public Task ExtractPackageAsync(string sourceFilePath, string destDirPath, IProgress<double> progress = null, CancellationToken cancellationToken = default)
            {
                File.Copy(sourceFilePath, Path.Combine(destDirPath, Path.GetFileName(Application.ExecutablePath)));
                return Task.CompletedTask;
            }
        }
    }
}
