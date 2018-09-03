using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StreamingRespirator.Utilities
{
    internal static class Hook
    {
        public static bool HookWinInet(int port, Process process)
            => HookProcess(port, process, "hookwininet");

        private static bool HookProcess(int port, Process process, string dll)
        {
            bool isX86;
            if (!Environment.Is64BitProcess)
                isX86 = true;
            else
            {
                var hProcess = NativeMethods.OpenProcess(NativeMethods.ProcessAccessFlags.All, false, process.Id);
                isX86 = NativeMethods.IsWow64Process(hProcess, out var isWow64) && isWow64;
                NativeMethods.CloseHandle(hProcess);
            }

            var sz = isX86 ? 32 : 64;

            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "hook", $"injector{sz}.exe"),
                Arguments = $"\"{process.Id}\" \"{port}\" \"{dll}{sz}.dll\"",
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            try
            {
                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                    return proc.ExitCode == 1;
                }
            }
            catch
            {
            }

            return false;
        }

        private class NativeMethods
        {
            [DllImport("kernel32.dll")]
            public static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

            [DllImport("kernel32.dll")]
            public static extern bool CloseHandle(IntPtr hHandle);

            [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsWow64Process([In] IntPtr processHandle, [Out, MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

            [Flags]
            public enum ProcessAccessFlags : uint
            {
                All = 0x001F0FFF,
                Terminate = 0x00000001,
                CreateThread = 0x00000002,
                VirtualMemoryOperation = 0x00000008,
                VirtualMemoryRead = 0x00000010,
                VirtualMemoryWrite = 0x00000020,
                DuplicateHandle = 0x00000040,
                CreateProcess = 0x000000080,
                SetQuota = 0x00000100,
                SetInformation = 0x00000200,
                QueryInformation = 0x00000400,
                QueryLimitedInformation = 0x00001000,
                Synchronize = 0x00100000
            }
        }
    }
}
