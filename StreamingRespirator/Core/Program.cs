using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace StreamingRespirator.Core
{
    internal static class Program
    {
        public const string MutexName = "{5FF75362-95BA-4399-8C77-C1A0C5B8A291}";

        public static readonly string CookiePath;

        static Program()
        {
            CookiePath = Path.ChangeExtension(Application.ExecutablePath, ".cookie");
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

#if !DEBUG
                if (!CheckUpdate())
                    return;
#endif

                var context = new MainContext();
                Application.Run(context);
                context.StopProxy();
            }
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
