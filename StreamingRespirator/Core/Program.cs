using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using StreamingRespirator.Properties;

namespace StreamingRespirator.Core
{
    internal static class Program
    {
        public const string MutexName = "{5FF75362-95BA-4399-8C77-C1A0C5B8A291}";

        public static readonly string ConfigPath;

        static Program()
        {
            ConfigPath  = Path.ChangeExtension(Application.ExecutablePath, ".cnf");
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

#if !DEBUG
                System.Net.WebRequest.DefaultWebProxy = null;

                if (!CheckUpdate())
                    return;
#endif

                Config.Load();

                MainContext context = null;
                try
                {
                    context = new MainContext();
                }
                catch
                {
                }

                if (context != null)
                {
                    Application.Run(context);
                    Config.Save();
                }

                context.StopProxy();
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
                catch
                {
                }

                return false;
            }

            return true;
        }
    }
}
