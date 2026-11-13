using TrueSpec.Core.Security;

namespace TrueSpec.Tests;

/// <summary>
/// Portable mode switched from inside the app (v2.1.1): the marker file
/// next to the exe is what the Settings page creates and removes. Pure
/// file I/O against temp folders, so these run on both legs.
/// </summary>
public class PortableModeTests
{
    private static string TempExeDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string MarkerPath(string exeDir) =>
        Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName);

    [Fact]
    public void GetStatus_NoMarkerNoEnv_IsNotPortableInDefaultFolder()
    {
        var exeDir = TempExeDir();

        try
        {
            var status = PortableMode.GetStatus(exeDir, environmentValue: null);

            Assert.False(status.IsPortable);
            Assert.False(status.MarkerPresent);
            Assert.False(status.EnvRequested);
            Assert.Equal(PathSafety.DefaultAppDataFolder(), status.DataFolder);
            Assert.Equal(Path.GetFullPath(exeDir), status.ExeFolder);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void GetStatus_MarkerPresent_IsPortableNextToExe()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(MarkerPath(exeDir), string.Empty);

        try
        {
            var status = PortableMode.GetStatus(exeDir, environmentValue: null);

            Assert.True(status.IsPortable);
            Assert.True(status.MarkerPresent);
            Assert.False(status.EnvRequested);
            Assert.Equal(Path.GetFullPath(exeDir), status.DataFolder);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void GetStatus_EnvVar_IsPortableWithoutMarker()
    {
        var exeDir = TempExeDir();

        try
        {
            var status = PortableMode.GetStatus(exeDir, environmentValue: "1");

            Assert.True(status.IsPortable);
            Assert.False(status.MarkerPresent);
            Assert.True(status.EnvRequested);
            Assert.Equal(Path.GetFullPath(exeDir), status.DataFolder);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void GetStatus_EnvVarOff_IsNotPortable()
    {
        var exeDir = TempExeDir();

        try
        {
            var status = PortableMode.GetStatus(exeDir, environmentValue: "0");

            Assert.False(status.IsPortable);
            Assert.False(status.EnvRequested);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void GetStatus_UnusableExeFolder_FallsBackToDefault()
    {
        var status = PortableMode.GetStatus(exeDirectory: string.Empty, environmentValue: null);

        Assert.False(status.IsPortable);
        Assert.Equal(PathSafety.DefaultAppDataFolder(), status.DataFolder);
    }

    [Fact]
    public void SetEnabled_Enable_CreatesMarkerAndReportsRestart()
    {
        var exeDir = TempExeDir();

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: true);

            Assert.True(result.Success);
            Assert.True(result.IsPortable);
            Assert.True(File.Exists(MarkerPath(exeDir)));
            Assert.Contains("Restart", result.Message, StringComparison.Ordinal);

            var status = PortableMode.GetStatus(exeDir, environmentValue: null);
            Assert.True(status.IsPortable);
            Assert.True(status.MarkerPresent);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_Enable_AlreadyOn_IsANoopSuccess()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(MarkerPath(exeDir), string.Empty);

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: true);

            Assert.True(result.Success);
            Assert.True(result.IsPortable);
            Assert.Contains("already on", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_Enable_MissingExeFolder_FailsHonestly()
    {
        var missing = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"), "no-such-dir");

        var result = PortableMode.SetEnabled(missing, enable: true);

        Assert.False(result.Success);
        Assert.False(result.IsPortable);
        Assert.Contains("could not be enabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetEnabled_Enable_MarkerPathIsADirectory_FailsHonestly()
    {
        var exeDir = TempExeDir();
        Directory.CreateDirectory(MarkerPath(exeDir));

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: true);

            Assert.False(result.Success);
            Assert.False(result.IsPortable);
            Assert.Contains("could not be enabled", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_Enable_ProtectedFolder_IsRefused()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(windows))
        {
            return;
        }

        var result = PortableMode.SetEnabled(windows, enable: true);

        Assert.False(result.Success);
        Assert.False(result.IsPortable);
        Assert.Contains("system or protected folder", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(MarkerPath(windows)));
    }

    [Fact]
    public void SetEnabled_Disable_RemovesMarker()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(MarkerPath(exeDir), string.Empty);

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: false);

            Assert.True(result.Success);
            Assert.False(result.IsPortable);
            Assert.False(File.Exists(MarkerPath(exeDir)));
            Assert.Contains("Restart", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_Disable_AlreadyOff_IsANoopSuccess()
    {
        var exeDir = TempExeDir();

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: false);

            Assert.True(result.Success);
            Assert.False(result.IsPortable);
            Assert.Contains("already off", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_Disable_EnvVarStillRequests_NamesTheEnvVar()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(MarkerPath(exeDir), string.Empty);

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: "1");

            Assert.True(result.Success);
            Assert.True(result.IsPortable);
            Assert.False(File.Exists(MarkerPath(exeDir)));
            Assert.Contains("TRUESPEC_PORTABLE", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_Disable_NoMarkerButEnvVar_NamesTheEnvVar()
    {
        var exeDir = TempExeDir();

        try
        {
            var result = PortableMode.SetEnabled(exeDir, enable: false, environmentValue: "yes");

            Assert.True(result.Success);
            Assert.True(result.IsPortable);
            Assert.Contains("TRUESPEC_PORTABLE", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }
}
