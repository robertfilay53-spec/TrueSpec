using TrueSpec.Core.Diagnostics;
using TrueSpec.Core.Security;
using TrueSpec.Core.Storage;

namespace TrueSpec.Tests;

/// <summary>
/// Security regression tests for the P4 hardening work. Every test here
/// guards a real attack surface: export paths, snapshot history and the
/// external tool runner.
/// </summary>
public class SecurityTests
{
    [Fact]
    public void ValidateExportFolder_AcceptsAbsolutePaths()
    {
        var path = Path.Combine(Path.GetTempPath(), "TrueSpec");
        Assert.Equal(path, PathSafety.ValidateExportFolder(path));
    }

    [Fact]
    public void ValidateExportFolder_AcceptsWindowsAbsolutePathOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Equal(@"C:\Users\buyer\Documents\TrueSpec",
            PathSafety.ValidateExportFolder(@"C:\Users\buyer\Documents\TrueSpec"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative\\folder")]
    [InlineData("relative/folder")]
    [InlineData(@"..\secret")]
    [InlineData(@"C:\Users\buyer\..\secret")]
    [InlineData("/tmp/../etc")]
    public void ValidateExportFolder_RejectsRelativeAndTraversalPaths(string path)
    {
        Assert.Null(PathSafety.ValidateExportFolder(path));
    }

    [Fact]
    public void ValidateExportFolder_RejectsNull()
    {
        Assert.Null(PathSafety.ValidateExportFolder(null));
    }

    [Theory]
    [InlineData("TrueSpec-report", "json")]
    [InlineData("TrueSpec-checklist", "html")]
    public void SafeReportFileName_ProducesTimestampedName(string prefix, string extension)
    {
        var name = PathSafety.SafeReportFileName(prefix, extension, new DateTime(2026, 11, 10, 12, 30, 0));

        Assert.Equal($"{prefix}-20261110-123000.{extension}", name);
    }

    [Fact]
    public void SafeReportFileName_StripsTraversalFromPrefix()
    {
        var name = PathSafety.SafeReportFileName(@"..\evil", "json", new DateTime(2026, 11, 10, 12, 30, 0));

        Assert.DoesNotContain("..", name);
        Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), name);
        Assert.DoesNotContain(Path.AltDirectorySeparatorChar.ToString(), name);
    }

    [Fact]
    public void SafeReportFileName_RejectsUnknownExtension()
    {
        Assert.Throws<ArgumentException>(() =>
            PathSafety.SafeReportFileName("report", "exe", DateTime.Now));
    }

    [Theory]
    [InlineData("netsh", "wlan show interfaces", true)]
    [InlineData("netsh", "wlan show drivers", true)]
    [InlineData("NETSH", "wlan show interfaces", true)]
    [InlineData("wmic", "cpu get name", false)]
    [InlineData("powershell", "Get-Process", false)]
    [InlineData("cmd", "/c dir", false)]
    [InlineData("netsh", "wlan show interfaces; rm -rf /", false)]
    [InlineData("netsh", "wlan show interfaces && whoami", false)]
    [InlineData("netsh", "wlan show interfaces | format-list", false)]
    [InlineData("netsh", "interface ip set address", false)]
    [InlineData("", "wlan show interfaces", false)]
    [InlineData("netsh", "", false)]
    public void IsAllowed_AllowsOnlyNetshReadOnlyQueries(string fileName, string arguments, bool expected)
    {
        Assert.Equal(expected, ExternalToolValidator.IsAllowed(fileName, arguments));
    }

    [Fact]
    public async Task SqliteStore_TreatsInjectionPayloadAsData()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var evilName = "' OR '1'='1'; DROP TABLE snapshots; --";
            var snapshot = TestSnapshots.Healthy() with { MachineName = evilName };
            var store = new SqliteSnapshotStore(databasePath);

            await store.InitializeAsync();
            await store.SaveAsync(snapshot);

            var history = await store.GetHistoryAsync();
            var saved = Assert.Single(history);
            Assert.Equal(evilName, saved.MachineName);

            // The table must still exist and the row must still be readable.
            var readBack = await store.GetSnapshotAsync(saved.Id);
            Assert.NotNull(readBack);
            Assert.Equal(evilName, readBack!.MachineName);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SqliteStore_GetHistoryLimitIsClamped()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();
            await store.SaveAsync(TestSnapshots.Healthy());

            // 0 and 10000 both clamp into the allowed 1-500 range.
            Assert.Single(await store.GetHistoryAsync(0));
            Assert.Single(await store.GetHistoryAsync(10000));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
