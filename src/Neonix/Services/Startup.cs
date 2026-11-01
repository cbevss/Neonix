using System;
using System.Diagnostics;

namespace Neonix.Services
{
    /// <summary>
    /// "Start with Windows". Neonix needs admin rights, and Windows ignores elevated apps in the
    /// normal Run key, so we register a logon task with highest privileges instead.
    /// </summary>
    public static class Startup
    {
        const string TaskName = "Neonix";

        public static bool Set(bool enabled)
        {
            var exe = Environment.ProcessPath ?? "";
            var args = enabled
                ? $"/Create /F /TN \"{TaskName}\" /SC ONLOGON /RL HIGHEST /TR \"\\\"{exe}\\\" --minimized\""
                : $"/Delete /F /TN \"{TaskName}\"";
            try
            {
                using var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                });
                p?.WaitForExit(5000);
                return p?.ExitCode == 0;
            }
            catch { return false; }
        }
    }
}
