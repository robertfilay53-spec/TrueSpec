using TrueSpec.Core.Settings;

namespace TrueSpec.Tests;

/// <summary>
/// Tests for the P4 settings store: sanitization of untrusted values and
/// the disk round-trip. Settings are user-editable JSON, so loading must
/// never trust the file.
/// </summary>
public class SettingsTests
{
    [Fact]
    public void Sanitized_ClampsRefreshInterval()
    {
        var settings = new AppSettings { RefreshIntervalSeconds = 0 }.Sanitized();
        Assert.Equal(1, settings.RefreshIntervalSeconds);

        settings = new AppSettings { RefreshIntervalSeconds = 999 }.Sanitized();
        Assert.Equal(30, settings.RefreshIntervalSeconds);

        settings = new AppSettings { RefreshIntervalSeconds = 5 }.Sanitized();
        Assert.Equal(5, settings.RefreshIntervalSeconds);
    }

    [Fact]
    public void Sanitized_RejectsTraversalExportFolder()
    {
        var settings = new AppSettings { ExportFolder = @"..\secret" }.Sanitized();
        Assert.Equal(string.Empty, settings.ExportFolder);

        settings = new AppSettings { ExportFolder = "relative\\path" }.Sanitized();
        Assert.Equal(string.Empty, settings.ExportFolder);
    }

    [Fact]
    public void Sanitized_KeepsValidAbsoluteExportFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "TrueSpec");
        var settings = new AppSettings { ExportFolder = folder }.Sanitized();
        Assert.Equal(folder, settings.ExportFolder);
    }

    [Fact]
    public void Store_RoundTripsSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");

        try
        {
            var store = new AppSettingsStore(path);
            var saved = new AppSettings
            {
                IsDarkTheme = false,
                RefreshIntervalSeconds = 5,
                ExportFolder = string.Empty,
            };
            store.Save(saved);

            var loaded = store.Load();
            Assert.False(loaded.IsDarkTheme);
            Assert.Equal(5, loaded.RefreshIntervalSeconds);
            Assert.Equal(string.Empty, loaded.ExportFolder);
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
    public void Store_LoadsDefaultsWhenFileIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var loaded = new AppSettingsStore(path).Load();

        Assert.True(loaded.IsDarkTheme);
        Assert.Equal(AppSettings.DefaultRefreshSeconds, loaded.RefreshIntervalSeconds);
        Assert.Equal(string.Empty, loaded.ExportFolder);
    }

    [Fact]
    public void Store_LoadsDefaultsWhenFileIsCorrupt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{ this is not json");

            var loaded = new AppSettingsStore(path).Load();
            Assert.True(loaded.IsDarkTheme);
            Assert.Equal(AppSettings.DefaultRefreshSeconds, loaded.RefreshIntervalSeconds);
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
    public void Store_SanitizesPoisonedFileOnLoad()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"IsDarkTheme\":true,\"RefreshIntervalSeconds\":9999,\"ExportFolder\":\"..\\\\secret\"}");

            var loaded = new AppSettingsStore(path).Load();
            Assert.Equal(30, loaded.RefreshIntervalSeconds);
            Assert.Equal(string.Empty, loaded.ExportFolder);
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
