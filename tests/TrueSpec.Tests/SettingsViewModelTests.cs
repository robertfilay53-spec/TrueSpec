using TrueSpec.App.ViewModels;
using TrueSpec.Core;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Sensors;
using TrueSpec.Core.Security;

namespace TrueSpec.Tests;

/// <summary>
/// Settings page, data-location section (v2.1.1): the portable-mode
/// status names the folder the app keeps its data in, and the toggle
/// creates or removes the marker file next to the exe. Windows leg only
/// (it drives TrueSpec.App's ViewModel), like the other ViewModel tests:
/// excluded from the net10.0 leg in the csproj.
/// </summary>
public class SettingsViewModelTests
{
    private static string TempExeDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static SettingsViewModel ViewModelFor(string exeDir, string? environmentValue = null)
    {
        var main = new MainViewModel(
            MachineScanService.CreateDefault(),
            new UnavailableSensorMonitor(),
            new BuyerCheckEvaluator());
        return new SettingsViewModel(main, exeDirectory: exeDir, environmentValue: environmentValue);
    }

    [Fact]
    public void Status_NoMarker_ShowsDefaultFolderAndOff()
    {
        var exeDir = TempExeDir();

        try
        {
            var viewModel = ViewModelFor(exeDir);

            Assert.Equal(PathSafety.DefaultAppDataFolder(), viewModel.DataFolderText);
            Assert.Contains("off", viewModel.PortableModeSummaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("default folder", viewModel.PortableModeSummaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Turn portable mode on", viewModel.PortableModeActionText);
            Assert.Equal(string.Empty, viewModel.PortableModeMessage);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Status_MarkerPresent_ShowsPortableFolderAndOn()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var viewModel = ViewModelFor(exeDir);

            Assert.Equal(Path.GetFullPath(exeDir), viewModel.DataFolderText);
            Assert.Contains("on", viewModel.PortableModeSummaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("next to the app", viewModel.PortableModeSummaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Turn portable mode off", viewModel.PortableModeActionText);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Status_EnvVar_ShowsEnvNote()
    {
        var exeDir = TempExeDir();

        try
        {
            var viewModel = ViewModelFor(exeDir, environmentValue: "1");

            Assert.Contains("environment variable", viewModel.PortableModeSummaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Path.GetFullPath(exeDir), viewModel.DataFolderText);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void TogglePortableMode_On_CreatesMarkerAndNamesRestart()
    {
        var exeDir = TempExeDir();

        try
        {
            var viewModel = ViewModelFor(exeDir);
            viewModel.TogglePortableModeCommand.Execute(null);

            Assert.True(File.Exists(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName)));
            Assert.Contains("Restart", viewModel.PortableModeMessage, StringComparison.Ordinal);
            Assert.Equal("Turn portable mode off", viewModel.PortableModeActionText);
            Assert.Equal(Path.GetFullPath(exeDir), viewModel.DataFolderText);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void TogglePortableMode_Off_RemovesMarker()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var viewModel = ViewModelFor(exeDir);
            viewModel.TogglePortableModeCommand.Execute(null);

            Assert.False(File.Exists(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName)));
            Assert.Contains("Restart", viewModel.PortableModeMessage, StringComparison.Ordinal);
            Assert.Equal("Turn portable mode on", viewModel.PortableModeActionText);
            Assert.Equal(PathSafety.DefaultAppDataFolder(), viewModel.DataFolderText);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void TogglePortableMode_OffWithEnvVar_NamesTheEnvVar()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var viewModel = ViewModelFor(exeDir, environmentValue: "1");
            viewModel.TogglePortableModeCommand.Execute(null);

            Assert.False(File.Exists(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName)));
            Assert.Contains("TRUESPEC_PORTABLE", viewModel.PortableModeMessage, StringComparison.Ordinal);
            Assert.Equal(Path.GetFullPath(exeDir), viewModel.DataFolderText);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    // Plug-ins, phase 1 (v2.1.5, plan Beyond v2.0 item 2): the Settings
    // page lists the plug-ins discovery found under this run's data
    // folder. The marker file turns portable mode on, so the data folder
    // (and the plug-ins folder under it) is the temp exe dir: the tests
    // stay hermetic and never touch the real app-data folder.
    private static string PluginFolderFor(string exeDir) =>
        Path.Combine(Path.GetFullPath(exeDir), "Plugins");

    private static void AddPlugin(string exeDir, string folderName, string manifestJson, bool withAssembly = true)
    {
        var folder = Path.Combine(PluginFolderFor(exeDir), folderName);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), manifestJson);
        if (withAssembly)
        {
            File.WriteAllText(Path.Combine(folder, "GpuZBridge.dll"), string.Empty);
        }
    }

    private const string ValidManifestJson = """
        {
          "id": "com.example.gpuz",
          "name": "GPU-Z bridge",
          "version": "1.0.0",
          "contractVersion": 1,
          "assembly": "GpuZBridge.dll",
          "collectors": [
            { "id": "gpu-extra", "name": "GPU extra", "category": "GPU" }
          ]
        }
        """;

    [Fact]
    public void Plugins_NoFolderFound_SaysNoneFound()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var viewModel = ViewModelFor(exeDir);

            Assert.Equal(PluginFolderFor(exeDir), viewModel.PluginFolderText);
            Assert.Equal("No plug-ins found.", viewModel.PluginsSummaryText);
            Assert.Empty(viewModel.DiscoveredPlugins);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Plugins_ValidPlugin_ListedAsReady()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName), string.Empty);
        AddPlugin(exeDir, "gpuz", ValidManifestJson);

        try
        {
            var viewModel = ViewModelFor(exeDir);

            Assert.Equal("1 plug-in found.", viewModel.PluginsSummaryText);
            var plugin = Assert.Single(viewModel.DiscoveredPlugins);
            Assert.Equal("GPU-Z bridge", plugin.DisplayName);
            Assert.Equal(TrueSpec.Core.Plugins.PluginStatus.Ready, plugin.Status);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Plugins_IncompatiblePlugin_ListedWithStatus()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName), string.Empty);
        AddPlugin(exeDir, "future", ValidManifestJson.Replace("\"contractVersion\": 1", "\"contractVersion\": 99"));

        try
        {
            var viewModel = ViewModelFor(exeDir);

            Assert.Equal("1 plug-in found.", viewModel.PluginsSummaryText);
            var plugin = Assert.Single(viewModel.DiscoveredPlugins);
            Assert.Equal(TrueSpec.Core.Plugins.PluginStatus.IncompatibleContract, plugin.Status);
            Assert.Equal("Needs a newer TrueSpec", plugin.StatusText);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Plugins_Refresh_RerunsDiscovery()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var viewModel = ViewModelFor(exeDir);
            Assert.Empty(viewModel.DiscoveredPlugins);

            AddPlugin(exeDir, "gpuz", ValidManifestJson);
            viewModel.RefreshPluginsCommand.Execute(null);

            Assert.Equal("1 plug-in found.", viewModel.PluginsSummaryText);
            Assert.Single(viewModel.DiscoveredPlugins);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Plugins_TogglePortableMode_MovesPluginFolder()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var viewModel = ViewModelFor(exeDir);
            Assert.Equal(PluginFolderFor(exeDir), viewModel.PluginFolderText);

            viewModel.TogglePortableModeCommand.Execute(null);

            Assert.Equal(Path.Combine(PathSafety.DefaultAppDataFolder(), "Plugins"), viewModel.PluginFolderText);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }
}
