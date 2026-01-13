using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Service.Helpers
{
    public static class CommandHelper
    {
        public static bool CommandExists(string cmd)
        {
            try
            {
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

                var psi = new ProcessStartInfo
                {
                    FileName = isWindows ? "cmd.exe" : "/bin/sh",
                    Arguments = isWindows
                        ? $"/c where {cmd}"
                        : $"-c \"command -v {cmd}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var p = Process.Start(psi)!;
                p.WaitForExit(2000);

                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
