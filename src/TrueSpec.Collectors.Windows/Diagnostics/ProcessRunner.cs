namespace TrueSpec.Collectors.Windows.Diagnostics;

using System.Diagnostics;
using TrueSpec.Core.Diagnostics;

/// <summary>
/// Runs a Windows tool such as netsh and returns its real output. Only the
/// allowlisted tools in <see cref="ExternalToolValidator"/> may run, with no
/// shell involved, and a run that does not finish in time is killed so a hung
/// tool can never hang the scan. Both output streams are read concurrently
/// so a full buffer on one stream cannot deadlock the other.
/// </summary>
internal static class ProcessRunner
{
    public static string? Run(string fileName, string arguments, int timeoutMilliseconds = 5000)
    {
        if (!ExternalToolValidator.IsAllowed(fileName, arguments))
        {
            return null;
        }

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

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // The process may already have exited between the check
                    // and the kill; either way it is gone.
                }

                return null;
            }

            // Both streams were read concurrently, so neither can block the
            // other. Wait briefly for the readers to drain.
            Task.WaitAll(new[] { outputTask, errorTask }, TimeSpan.FromSeconds(2));
            return outputTask.IsCompletedSuccessfully ? outputTask.Result : null;
        }
        catch
        {
            return null;
        }
    }
}
