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
    public void DefaultExportFolder_UsesDownloadsWhenItExists()
    {
        // v1.4.1 (F10): the default export folder is Downloads when it
        // exists, because that is where people look for a file they just
        // saved. The v1.4.0 laptop test reported export as broken when the
        // files were in fact written to Documents/TrueSpec.
        var profile = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var documents = Path.Combine(profile, "Documents");
        try
        {
            Directory.CreateDirectory(Path.Combine(profile, "Downloads"));

            Assert.Equal(
                Path.Combine(profile, "Downloads"),
                PathSafety.ResolveDefaultExportFolder(profile, documents));
        }
        finally
        {
            if (Directory.Exists(profile))
            {
                Directory.Delete(profile, recursive: true);
            }
        }
    }

    [Fact]
    public void DefaultExportFolder_FallsBackToDocumentsWhenDownloadsIsMissing()
    {
        var profile = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var documents = Path.Combine(profile, "Documents");
        try
        {
            Directory.CreateDirectory(documents);

            Assert.Equal(
                Path.Combine(documents, "TrueSpec"),
                PathSafety.ResolveDefaultExportFolder(profile, documents));
        }
        finally
        {
            if (Directory.Exists(profile))
            {
                Directory.Delete(profile, recursive: true);
            }
        }
    }

    [Fact]
    public void DefaultExportFolder_FallsBackToProfileWhenDocumentsIsMissing()
    {
        var profile = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(profile);

            Assert.Equal(
                Path.Combine(profile, "TrueSpec"),
                PathSafety.ResolveDefaultExportFolder(profile, null));
        }
        finally
        {
            if (Directory.Exists(profile))
            {
                Directory.Delete(profile, recursive: true);
            }
        }
    }

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

    [Fact]
    public void PathSafety_RejectsSymlinkedPathsThatEscapeTheBaseFolder()
    {
        // Hardening 6g: containment is checked after resolving symlinks
        // and junctions, so a link inside the app-data folder that points
        // outside cannot smuggle the database or the sensor log out.
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var inside = Path.Combine(root, "inside");
        var outside = Path.Combine(root, "outside");
        Directory.CreateDirectory(inside);
        Directory.CreateDirectory(outside);
        try
        {
            var link = Path.Combine(inside, "link");
            File.CreateSymbolicLink(link, outside);

            Assert.Null(PathSafety.ValidateDatabasePath(Path.Combine(link, "history.db"), inside));
            Assert.Null(PathSafety.ValidateSensorLogPath(Path.Combine(link, "sensor-log.csv"), inside));

            // The export folder is user-picked, so a link to an ordinary
            // folder is allowed, but it is resolved first: the validator
            // returns the real target, never the link path.
            var resolved = PathSafety.ValidateExportFolder(link);
            Assert.NotNull(resolved);
            Assert.DoesNotContain("link", resolved, StringComparison.OrdinalIgnoreCase);

            // The same paths without the link still validate.
            Assert.NotNull(PathSafety.ValidateDatabasePath(Path.Combine(inside, "history.db"), inside));
            Assert.NotNull(PathSafety.ValidateSensorLogPath(Path.Combine(inside, "sensor-log.csv"), inside));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
