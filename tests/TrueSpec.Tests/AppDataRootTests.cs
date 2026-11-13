using TrueSpec.Core.Security;

namespace TrueSpec.Tests;

/// <summary>
/// The data-root abstraction (v2.0 prerequisite, review section 9):
/// portable mode via the marker file or the env var, the default root
/// otherwise, and validators that accept the portable root.
/// </summary>
public class AppDataRootTests
{
    private static string TempExeDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Resolve_WithoutMarkerOrEnv_UsesTheDefaultAppDataFolder()
    {
        var exeDir = TempExeDir();

        try
        {
            var root = AppDataRoot.Resolve(exeDirectory: exeDir, environmentValue: null);

            Assert.False(root.IsPortable);
            Assert.Equal(PathSafety.DefaultAppDataFolder(), root.Folder);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_MarkerFileNextToExe_SelectsThePortableRoot()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var root = AppDataRoot.Resolve(exeDirectory: exeDir, environmentValue: null);

            Assert.True(root.IsPortable);
            Assert.Equal(Path.GetFullPath(exeDir), root.Folder);
            Assert.Equal(Path.Combine(root.Folder, "history.db"), root.DatabasePath);
            Assert.Equal(Path.Combine(root.Folder, "settings.json"), root.SettingsPath);
            Assert.Equal(Path.Combine(root.Folder, "sensor-log.csv"), root.SensorLogPath);
            Assert.Equal(Path.Combine(root.Folder, "startup.log"), root.StartupLogPath);
            Assert.Equal(Path.Combine(root.Folder, "window-state.json"), root.WindowStatePath);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_EnvironmentVariable_SelectsThePortableRoot()
    {
        var exeDir = TempExeDir();

        try
        {
            foreach (var value in new[] { "1", "true", "True", "TRUE", "yes", "on" })
            {
                var root = AppDataRoot.Resolve(exeDirectory: exeDir, environmentValue: value);

                Assert.True(root.IsPortable, $"env value '{value}' should select portable mode");
                Assert.Equal(Path.GetFullPath(exeDir), root.Folder);
            }
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_EnvironmentVariableOff_DoesNotSelectPortableMode()
    {
        var exeDir = TempExeDir();

        try
        {
            foreach (var value in new[] { "0", "false", "no", "off", "", "   " })
            {
                var root = AppDataRoot.Resolve(exeDirectory: exeDir, environmentValue: value);

                Assert.False(root.IsPortable, $"env value '{value}' must not select portable mode");
            }
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_MarkerFileInAnotherFolder_DoesNotSelectPortableMode()
    {
        // The marker only counts next to the exe: a stray file in some
        // other folder (or the working directory) must not flip the mode.
        var exeDir = TempExeDir();
        var otherDir = TempExeDir();
        File.WriteAllText(Path.Combine(otherDir, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var root = AppDataRoot.Resolve(exeDirectory: exeDir, environmentValue: null);

            Assert.False(root.IsPortable);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
            Directory.Delete(otherDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_PortableRootInsideAWindowsFolder_FallsBackToDefault()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windows))
        {
            return;
        }

        var root = AppDataRoot.Resolve(exeDirectory: windows, environmentValue: "1");

        Assert.False(root.IsPortable);
        Assert.Equal(PathSafety.DefaultAppDataFolder(), root.Folder);
    }

    [Fact]
    public void ValidateAppDataRoot_AcceptsAPortableRoot()
    {
        var exeDir = TempExeDir();

        try
        {
            Assert.Equal(Path.GetFullPath(exeDir), PathSafety.ValidateAppDataRoot(exeDir));
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateAppDataRoot_RejectsRelativePathsTraversalAndSystemFolders()
    {
        Assert.Null(PathSafety.ValidateAppDataRoot("relative\\folder"));
        Assert.Null(PathSafety.ValidateAppDataRoot(null));
        Assert.Null(PathSafety.ValidateAppDataRoot("   "));
        Assert.Null(PathSafety.ValidateAppDataRoot(
            Path.Combine(Path.GetTempPath(), "..", "escape")));

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windows))
        {
            Assert.Null(PathSafety.ValidateAppDataRoot(windows));
            Assert.Null(PathSafety.ValidateAppDataRoot(Path.Combine(windows, "System32")));
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            Assert.Null(PathSafety.ValidateAppDataRoot(programFiles));
        }
    }

    [Fact]
    public void Validators_AcceptFilesInsideThePortableRoot()
    {
        var exeDir = TempExeDir();

        try
        {
            var root = AppDataRoot.Resolve(exeDirectory: exeDir, environmentValue: "1");
            Assert.True(root.IsPortable);

            Assert.NotNull(PathSafety.ValidateDatabasePath(root.DatabasePath, root.Folder));
            Assert.NotNull(PathSafety.ValidateSensorLogPath(root.SensorLogPath, root.Folder));

            // ...and still reject a file that escapes the portable root.
            Assert.Null(PathSafety.ValidateDatabasePath(
                Path.Combine(Path.GetTempPath(), "history.db"), root.Folder));
            Assert.Null(PathSafety.ValidateSensorLogPath(
                Path.Combine(Path.GetTempPath(), "sensor-log.csv"), root.Folder));
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Portable_WritesSettingsAndReadsThemBack()
    {
        var exeDir = TempExeDir();

        try
        {
            var root = AppDataRoot.Resolve(exeDirectory: exeDir, environmentValue: "1");
            var store = new TrueSpec.Core.Settings.AppSettingsStore(root.SettingsPath);

            Assert.True(store.Save(TrueSpec.Core.Settings.AppSettings.Default));
            Assert.True(File.Exists(root.SettingsPath));
            Assert.Equal(
                TrueSpec.Core.Settings.AppSettings.Default.IsDarkTheme,
                store.Load().IsDarkTheme);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }
}
