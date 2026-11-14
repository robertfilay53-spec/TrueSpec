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
    public void Sanitized_LanguageDefaultsToEnglish()
    {
        Assert.Equal("en", AppSettings.Default.LanguageCode);
        Assert.Equal("en", AppSettings.Default.Sanitized().LanguageCode);
    }

    [Theory]
    [InlineData("de", "de")]
    [InlineData("DE", "de")]
    [InlineData(" de ", "de")]
    [InlineData("de-DE", "de")]
    public void Sanitized_NormalizesSupportedLanguageCode(string input, string expected)
    {
        var settings = new AppSettings { LanguageCode = input }.Sanitized();
        Assert.Equal(expected, settings.LanguageCode);
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitized_UnknownLanguageFallsBackToEnglish(string input)
    {
        var settings = new AppSettings { LanguageCode = input }.Sanitized();
        Assert.Equal("en", settings.LanguageCode);
    }

    [Fact]
    public void Sanitized_NullLanguageFallsBackToEnglish()
    {
        var settings = new AppSettings { LanguageCode = null! }.Sanitized();
        Assert.Equal("en", settings.LanguageCode);
    }

    [Fact]
    public void Store_RoundTripsLanguageCode()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");

        try
        {
            var store = new AppSettingsStore(path);
            store.Save(new AppSettings { LanguageCode = "de" });

            var loaded = store.Load();
            Assert.Equal("de", loaded.LanguageCode);
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

    [Fact]
    public void Sanitized_ThresholdDefaultsToOff()
    {
        var settings = AppSettings.Default.Sanitized();
        Assert.Null(settings.CpuTemperatureThresholdC);
        Assert.True(settings.SensorLoggingEnabled);
        Assert.True(settings.HardwareDatabaseEnabled);
        Assert.Equal(AppSettings.DefaultCpuTemperatureThresholdSeconds, settings.CpuTemperatureThresholdSeconds);
    }

    [Theory]
    [InlineData(200, 120)]
    [InlineData(10, 30)]
    [InlineData(90, 90)]
    [InlineData(85.5, 85.5)]
    public void Sanitized_ClampsCpuTemperatureThreshold(double input, double expected)
    {
        var settings = new AppSettings { CpuTemperatureThresholdC = input }.Sanitized();
        Assert.Equal(expected, settings.CpuTemperatureThresholdC);
    }

    [Fact]
    public void Sanitized_NullOrNanThresholdStaysOff()
    {
        Assert.Null(new AppSettings { CpuTemperatureThresholdC = null }.Sanitized().CpuTemperatureThresholdC);
        Assert.Null(new AppSettings { CpuTemperatureThresholdC = double.NaN }.Sanitized().CpuTemperatureThresholdC);
        Assert.Null(new AppSettings { CpuTemperatureThresholdC = double.PositiveInfinity }.Sanitized().CpuTemperatureThresholdC);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(9999, 300)]
    [InlineData(60, 60)]
    public void Sanitized_ClampsCpuTemperatureThresholdSeconds(int input, int expected)
    {
        var settings = new AppSettings { CpuTemperatureThresholdSeconds = input }.Sanitized();
        Assert.Equal(expected, settings.CpuTemperatureThresholdSeconds);
    }

    [Fact]
    public void Store_RoundTripsSensorSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");

        try
        {
            var store = new AppSettingsStore(path);
            var saved = new AppSettings
            {
                IsDarkTheme = true,
                RefreshIntervalSeconds = 5,
                ExportFolder = string.Empty,
                SensorLoggingEnabled = false,
                HardwareDatabaseEnabled = false,
                CpuTemperatureThresholdC = 90,
                CpuTemperatureThresholdSeconds = 60,
            };
            Assert.True(store.Save(saved));

            var loaded = store.Load();
            Assert.False(loaded.SensorLoggingEnabled);
            Assert.False(loaded.HardwareDatabaseEnabled);
            Assert.Equal(90, loaded.CpuTemperatureThresholdC);
            Assert.Equal(60, loaded.CpuTemperatureThresholdSeconds);
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
    public void Store_LoadsSensorDefaultsFromAnOldSettingsFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"isDarkTheme\":true,\"refreshIntervalSeconds\":2,\"exportFolder\":\"\"}");

            var loaded = new AppSettingsStore(path).Load();
            Assert.True(loaded.SensorLoggingEnabled);
            Assert.True(loaded.HardwareDatabaseEnabled);
            Assert.Null(loaded.CpuTemperatureThresholdC);
            Assert.Equal(AppSettings.DefaultCpuTemperatureThresholdSeconds, loaded.CpuTemperatureThresholdSeconds);
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
