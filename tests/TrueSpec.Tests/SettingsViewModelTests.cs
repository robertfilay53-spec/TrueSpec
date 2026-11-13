using TrueSpec.App.Services;
using TrueSpec.App.ViewModels;
using TrueSpec.Core;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Sensors;
using TrueSpec.Core.Security;
using TrueSpec.Core.Settings;

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

    /// <summary>
    /// Localisation, phase 1 (v2.1.7): a shell wired to a temp settings
    /// store, so a language change is fully isolated — the real settings
    /// file is never touched. The store is pre-seeded with
    /// <paramref name="seedLanguage"/> when one is given.
    /// </summary>
    private static (MainViewModel Main, SettingsViewModel Settings, string Root) ViewModelWithTempStore(string? seedLanguage = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
        if (seedLanguage is not null)
        {
            Assert.True(store.Save(new AppSettings { LanguageCode = seedLanguage }));
        }

        var main = new MainViewModel(
            Array.Empty<ICollector>(),
            new UnavailableSensorMonitor(),
            new BuyerCheckEvaluator(),
            store,
            new ShellExportFileOpener());
        var settings = new SettingsViewModel(main, exeDirectory: root);
        return (main, settings, root);
    }

    /// <summary>A fresh shell built from an existing temp store (a restart).</summary>
    private static (MainViewModel Main, SettingsViewModel Settings) ViewModelFromStore(string root)
    {
        var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
        var main = new MainViewModel(
            Array.Empty<ICollector>(),
            new UnavailableSensorMonitor(),
            new BuyerCheckEvaluator(),
            store,
            new ShellExportFileOpener());
        return (main, new SettingsViewModel(main, exeDirectory: root));
    }

    private static void DeleteTree(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Language_DefaultsToEnglish()
    {
        var (_, settings, root) = ViewModelWithTempStore();

        try
        {
            Assert.Equal("en", settings.LanguageCode);
            Assert.Equal("English", settings.LanguageText);
            Assert.Equal(new[] { "en", "de" }, settings.LanguageOptions);
            Assert.Equal("Language", settings.LanguageTitleText);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Language_PickingGerman_RebuildsRailInGermanAndKeepsThePage()
    {
        var (main, settings, root) = ViewModelWithTempStore();

        try
        {
            settings.LanguageCode = "de";

            Assert.Equal("Deutsch", settings.LanguageText);
            Assert.Equal("Sprache", settings.LanguageTitleText);
            Assert.Equal(
                "Wählen Sie die Sprache für die Navigationsleiste. Die Sprache wird sofort angewendet.",
                settings.LanguageDescriptionText);
            Assert.Equal(
                "Die Navigation ist übersetzt. Detailseiten sind noch auf Englisch und folgen in späteren Versionen.",
                settings.LanguageNoteText);

            var labels = main.NavItems.Select(i => i.Label).ToList();
            Assert.Equal(22, labels.Count);
            Assert.Contains("Übersicht", labels);
            Assert.Contains("Einstellungen", labels);
            Assert.DoesNotContain("Overview", labels);

            // The user stays on the page they were on: the rail rebuild
            // re-selects by key and never navigates away.
            Assert.Equal("overview", main.SelectedNavItem?.Key);
            Assert.IsType<OverviewViewModel>(main.CurrentPage);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Language_PickingGerman_PersistsAcrossRestart()
    {
        var (_, settings, root) = ViewModelWithTempStore();

        try
        {
            settings.LanguageCode = "de";

            var loaded = new AppSettingsStore(Path.Combine(root, "settings.json")).Load();
            Assert.Equal("de", loaded.LanguageCode);

            // A fresh shell built from the same store loads the German rail.
            var (freshMain, _) = ViewModelFromStore(root);
            Assert.Contains("Übersicht", freshMain.NavItems.Select(i => i.Label));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Language_GermanStoredBeforeConstruction_LoadsGermanRailWithoutRecursion()
    {
        // The guard this pins: picking German fires the language hook
        // while the Settings page is still being constructed (the hook
        // saves, the save applies settings, the apply rebuilds the rail,
        // and the rebuild re-selects — which must never re-enter page
        // creation, because GetOrCreate caches a page only after its
        // factory returns). Without the guard this test stack-overflows.
        var (main, settings, root) = ViewModelWithTempStore(seedLanguage: "de");

        try
        {
            Assert.Equal("de", settings.LanguageCode);
            Assert.Contains("Übersicht", main.NavItems.Select(i => i.Label));
            Assert.Contains("Einstellungen", main.NavItems.Select(i => i.Label));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Language_UnknownStoredCode_FallsBackToEnglish()
    {
        var (main, settings, root) = ViewModelWithTempStore(seedLanguage: "xx");

        try
        {
            Assert.Equal("en", settings.LanguageCode);
            Assert.Contains("Overview", main.NavItems.Select(i => i.Label));
            Assert.DoesNotContain("Übersicht", main.NavItems.Select(i => i.Label));
        }
        finally
        {
            DeleteTree(root);
        }
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
