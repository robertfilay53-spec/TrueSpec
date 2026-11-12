using TrueSpec.Core.Security;
using TrueSpec.Core.Settings;

namespace TrueSpec.Tests;

/// <summary>
/// Regression tests for the Nov 12, 2026 hardening pass: the per-user
/// single-instance identity (a fixed mutex name lets any local process
/// squat it and block launch) and the unpredictable settings temp file
/// (a fixed "settings.json.tmp" is a plantable path). The raw-device
/// drive-number guard lives in StorageCollectorTests, the Windows-leg file
/// that can see the internal SMART reader.
/// </summary>
public class HardeningTests
{
    [Fact]
    public void PerUserSuffix_IsStableLowercaseHex()
    {
        var first = SingleInstanceIdentity.PerUserSuffix();
        var second = SingleInstanceIdentity.PerUserSuffix();

        Assert.Equal(first, second);
        Assert.Equal(16, first.Length);
        Assert.Matches("^[0-9a-f]{16}$", first);
    }

    [Fact]
    public void PerUserSuffix_DoesNotLeakTheUsername()
    {
        // A 1-3 character username can appear in 16 hex characters by
        // coincidence, so only a longer name proves a leak.
        if (Environment.UserName.Length < 4)
        {
            return;
        }

        var suffix = SingleInstanceIdentity.PerUserSuffix();

        Assert.DoesNotContain(Environment.UserName.ToLowerInvariant(), suffix);
    }

    [Fact]
    public void AppSettingsStore_SaveLeavesNoTempFileBehind()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "settings.json");

        try
        {
            var store = new AppSettingsStore(filePath);

            Assert.True(store.Save(new AppSettings { IsDarkTheme = false, RefreshIntervalSeconds = 5 }));
            Assert.Equal("settings.json", Path.GetFileName(Assert.Single(Directory.GetFiles(directory))));
            Assert.Equal(5, store.Load().RefreshIntervalSeconds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AppSettingsStore_SaveIgnoresStaleFixedTempFile()
    {
        // The old code always wrote to "settings.json.tmp". A junk file
        // left there by a crashed older run (or planted) must not affect
        // the save, and must be left alone: the new temp name is random.
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "settings.json");
        var staleTemp = filePath + ".tmp";

        try
        {
            File.WriteAllText(staleTemp, "junk from an older run");
            var store = new AppSettingsStore(filePath);

            Assert.True(store.Save(new AppSettings { IsDarkTheme = true, RefreshIntervalSeconds = 7 }));
            Assert.Equal(7, store.Load().RefreshIntervalSeconds);
            Assert.Equal("junk from an older run", File.ReadAllText(staleTemp));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
