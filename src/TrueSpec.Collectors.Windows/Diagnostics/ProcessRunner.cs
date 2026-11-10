namespace TrueSpec.Collectors.Windows.Diagnostics;

using System.Diagnostics;

/// <summary>Runs a Windows tool such as netsh and returns its real output.</summary>
internal static class ProcessRunner
{
    public static string? Run(string fileName, string arguments, int timeoutMilliseconds = 5000)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit(timeoutMilliseconds);
            return output;
        }
        catch
        {
            return null;
        }
    }
}
