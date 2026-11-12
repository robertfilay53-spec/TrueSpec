using TrueSpec.Core.Security;

namespace TrueSpec.Tests;

/// <summary>
/// TEMPORARY diagnostic for the Windows CI failure of
/// PathSafety_RejectsSymlinkedPathsThatEscapeTheBaseFolder: the
/// validator returns non-null through a symlinked folder on Windows
/// while the same code passes on Linux. This test fails on purpose
/// with the observed values in its message so the CI log shows what
/// the link looks like to each API. DELETE before merging.
/// </summary>
public class SymlinkDiagTests
{
    [Fact]
    public void Diag_SymlinkResolutionOnWindows()
    {
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var inside = Path.Combine(root, "inside");
        var outside = Path.Combine(root, "outside");
        Directory.CreateDirectory(inside);
        Directory.CreateDirectory(outside);
        try
        {
            var link = Path.Combine(inside, "link");
            File.CreateSymbolicLink(link, outside);

            static string Try(Func<string, bool, FileSystemInfo?> resolve, string path)
            {
                try
                {
                    return resolve(path, true)?.FullName ?? "<null>";
                }
                catch (Exception ex)
                {
                    return $"<{ex.GetType().Name}: {ex.Message}>";
                }
            }

            var dbPath = Path.Combine(link, "history.db");
            var message =
                $"link={link} outside={outside} " +
                $"Directory.Exists(link)={Directory.Exists(link)} File.Exists(link)={File.Exists(link)} " +
                $"File.ResolveLinkTarget={Try(File.ResolveLinkTarget, link)} " +
                $"Directory.ResolveLinkTarget={Try(Directory.ResolveLinkTarget, link)} " +
                $"ResolveLinks(db)={PathSafety.ResolveLinks(dbPath)} " +
                $"ValidateDatabasePath={PathSafety.ValidateDatabasePath(dbPath, inside) ?? "<null>"} " +
                $"ValidateSensorLogPath={PathSafety.ValidateSensorLogPath(Path.Combine(link, "sensor-log.csv"), inside) ?? "<null>"} " +
                $"ValidateExportFolder={PathSafety.ValidateExportFolder(link) ?? "<null>"}";
            Assert.Fail(message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
