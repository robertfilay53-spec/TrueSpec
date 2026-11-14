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
            Assert.Equal(new[] { "en", "de", "fr", "es" }, settings.LanguageOptions);
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
                "Navigation, Einstellungen, Komponentendetailseiten, die Übersichtsseite, die Verlaufsseite, die Berichtsseite, die Testseite, die Seite „Neuer Laptop Check\", die Absturzverlaufsseite, die Vergleichsseite und die Geräteparkseite sind übersetzt. Alle Seiten sind jetzt übersetzt.",
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
    public void Language_PickingFrench_RebuildsRailInFrenchAndKeepsThePage()
    {
        var (main, settings, root) = ViewModelWithTempStore();

        try
        {
            settings.LanguageCode = "fr";

            Assert.Equal("Français", settings.LanguageText);
            Assert.Equal("Langue", settings.LanguageTitleText);
            Assert.Equal(
                "Choisissez la langue de la barre de navigation. La langue s'applique immédiatement.",
                settings.LanguageDescriptionText);
            Assert.Equal(
                "La barre de navigation, la page Paramètres, les pages de détail des composants, la page Vue d'ensemble, la page Historique, la page Rapport, la page Tests, la page Vérification d'ordinateur portable neuf, la page Historique des incidents, la page Comparer et la page Parc sont traduites. Toutes les pages sont désormais traduites.",
                settings.LanguageNoteText);

            var labels = main.NavItems.Select(i => i.Label).ToList();
            Assert.Equal(22, labels.Count);
            Assert.Contains("Vue d'ensemble", labels);
            Assert.Contains("Paramètres", labels);
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
    public void Language_PickingSpanish_RebuildsRailInSpanishAndKeepsThePage()
    {
        var (main, settings, root) = ViewModelWithTempStore();

        try
        {
            settings.LanguageCode = "es";

            Assert.Equal("Español", settings.LanguageText);
            Assert.Equal("Idioma", settings.LanguageTitleText);
            Assert.Equal(
                "Elige el idioma de la barra de navegación. El idioma se aplica de inmediato.",
                settings.LanguageDescriptionText);
            Assert.Equal(
                "La barra de navegación, la página Configuración, las páginas de detalle de componentes, la página Vista general, la página Historial, la página Informe, la página Pruebas, la página Comprobación de comprador, la página Historial de bloqueos, la página Comparar y la página Flota están traducidas. Todas las páginas están traducidas.",
                settings.LanguageNoteText);

            var labels = main.NavItems.Select(i => i.Label).ToList();
            Assert.Equal(22, labels.Count);
            Assert.Contains("Vista general", labels);
            Assert.Contains("Configuración", labels);
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

    // Localisation, phase 2 (v2.1.8): the whole Settings page in German.
    // A shell seeded with German renders every card the page owns; the
    // only English leftovers are the Core diagnostic strings (the
    // plug-in list's per-item status/reason and the portable-toggle
    // result message), pinned here as the documented exception.
    [Fact]
    public void Settings_GermanSeed_RendersWholePageInGerman()
    {
        var (_, settings, root) = ViewModelWithTempStore(seedLanguage: "de");

        try
        {
            Assert.Equal("EINSTELLUNGEN", settings.SettingsEyebrowText);
            Assert.Equal("Einstellungen", settings.SettingsTitleText);
            Assert.Equal(
                "Kleine, ehrliche Einstellungen. Jede Änderung wird sofort gespeichert und angewendet. Nichts hier telefoniert nach Hause.",
                settings.SettingsIntroText);

            Assert.Equal("Darstellung", settings.AppearanceTitleText);
            Assert.Equal("Dunkel ist das Standarddesign. Hell ist für helle Räume verfügbar.", settings.AppearanceNoteText);
            Assert.Equal("Dunkel (Standard)", settings.ThemeText);
            Assert.Equal("Dunkel", settings.ThemeDarkText);
            Assert.Equal("Hell", settings.ThemeLightText);

            Assert.Equal("Aktualisierungsrate der Live-Sensoren", settings.RefreshTitleText);
            Assert.Equal("Wie oft der Live-Sensorstreifen neu liest. Schneller ist lebhafter, langsamer schont die CPU.", settings.RefreshNoteText);
            Assert.Equal(new[] { "1 Sekunde", "2 Sekunden", "5 Sekunden", "10 Sekunden", "30 Sekunden" },
                settings.RefreshRateOptions.Select(o => o.Text).ToList());
            Assert.Equal(new[] { 1, 2, 5, 10, 30 },
                settings.RefreshRateOptions.Select(o => o.Value).ToList());

            Assert.Equal("Sensorprotokollierung und Warnungen", settings.LoggingTitleText);
            Assert.Equal("Sensormesswerte aufzeichnen", settings.RecordTitleText);
            Assert.Equal("Ein", settings.ToggleOnText);
            Assert.Equal("Aus", settings.ToggleOffText);
            Assert.Equal("Warnung bei CPU-Temperatur", settings.AlertTitleText);
            Assert.Equal("Keine Temperaturwarnung eingestellt.", settings.AlertSummaryText);
            Assert.Equal("Warnung oberhalb (°C)", settings.AlertAboveLabelText);
            Assert.Equal("Muss darüber bleiben für", settings.AlertDurationLabelText);
            Assert.Equal(new[] { "15 Sekunden", "30 Sekunden", "60 Sekunden", "120 Sekunden" },
                settings.ThresholdDurationOptions.Select(o => o.Text).ToList());

            Assert.Equal("Hardware-Datenbank", settings.HardwareDatabaseTitleText);
            Assert.Equal(
                "Zeigt den Nenn-Boost-Takt aus einer kleinen mitgelieferten Datenbank mit CPU- und GPU-Daten, wenn Ihr Prozessor oder Ihre Grafikkarte enthalten ist. Die Datenbank ist optional und ersetzt keinen gemessenen Wert. Wird nach einem Neustart wirksam.",
                settings.HardwareDatabaseNoteText);
            Assert.StartsWith("Datensatz v1.14.0: ", settings.HardwareDatabaseVersionText, StringComparison.Ordinal);

            Assert.Equal("Ordner für Berichtsexporte", settings.ExportTitleText);
            Assert.EndsWith(" (Standard)", settings.ExportFolderText, StringComparison.Ordinal);
            Assert.Equal("Ordner wählen...", settings.ExportChooseText);
            Assert.Equal("Auf Standard zurücksetzen", settings.ExportResetText);

            Assert.Equal("Datenspeicherort (tragbarer Modus)", settings.DataLocationTitleText);
            Assert.Equal("Der tragbare Modus ist aus. Daten werden im Standardordner gespeichert.", settings.PortableModeSummaryText);
            Assert.Equal("Tragbaren Modus einschalten", settings.PortableModeActionText);

            Assert.Equal("Plug-ins", settings.PluginsTitleText);
            Assert.Equal("Keine Plug-ins gefunden.", settings.PluginsSummaryText);
            Assert.Equal("Plug-ins aktualisieren", settings.PluginsRefreshText);

            Assert.Equal("Datenschutz", settings.PrivacyTitleText);
            Assert.Contains("keine Telemetrie", settings.PrivacyText, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_PickingGerman_RerendersWholePageAndBack()
    {
        var (_, settings, root) = ViewModelWithTempStore();

        try
        {
            Assert.Equal("Settings", settings.SettingsTitleText);
            Assert.Equal("Dark (default)", settings.ThemeText);
            Assert.Contains("5 seconds", settings.RefreshRateOptions.Select(o => o.Text));

            settings.LanguageCode = "de";

            Assert.Equal("Einstellungen", settings.SettingsTitleText);
            Assert.Equal("Dunkel (Standard)", settings.ThemeText);
            Assert.Contains("5 Sekunden", settings.RefreshRateOptions.Select(o => o.Text));
            Assert.Equal("Keine Temperaturwarnung eingestellt.", settings.AlertSummaryText);
            Assert.Equal("Keine Plug-ins gefunden.", settings.PluginsSummaryText);

            settings.LanguageCode = "en";

            Assert.Equal("Settings", settings.SettingsTitleText);
            Assert.Equal("Dark (default)", settings.ThemeText);
            Assert.Contains("5 seconds", settings.RefreshRateOptions.Select(o => o.Text));
            Assert.Equal("No temperature alert set.", settings.AlertSummaryText);
            Assert.Equal("No plug-ins found.", settings.PluginsSummaryText);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_German_AlertSummaryShowsNumbers()
    {
        var (_, settings, root) = ViewModelWithTempStore(seedLanguage: "de");

        try
        {
            settings.CpuTemperatureThresholdText = "90";
            settings.CpuTemperatureThresholdSeconds = 30;
            settings.CpuTemperatureAlertEnabled = true;

            // Numbers keep the app's formatting inside the German
            // sentence: 90 stays "90", never "90,0".
            Assert.Equal(
                "Warnung, wenn die CPU-Temperatur 90 °C für 30 Sekunden erreicht.",
                settings.AlertSummaryText);

            settings.CpuTemperatureThresholdText = "abc";
            Assert.Equal(
                "Geben Sie eine Temperatur zwischen 30 und 120 °C ein.",
                settings.ThresholdMessage);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_German_PluginsAreGerman()
    {
        // Localisation, phase 3 (v2.1.9): the per-item status/reason
        // are Core diagnostic strings that now take a language code,
        // so a German shell lists German plug-ins. (v2.1.8 pinned the
        // opposite: they stayed English then. This test replaces that
        // pin.) Manual setup: the store is seeded German, and the
        // marker file makes the temp dir the data folder, so the
        // plug-ins folder under it is hermetic (same pattern as the
        // v2.1.5 tests).
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, AppDataRoot.PortableMarkerFileName), string.Empty);
        AddPlugin(root, "gpuz", ValidManifestJson);

        try
        {
            var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
            Assert.True(store.Save(new AppSettings { LanguageCode = "de" }));
            var main = new MainViewModel(
                Array.Empty<ICollector>(),
                new UnavailableSensorMonitor(),
                new BuyerCheckEvaluator(),
                store,
                new ShellExportFileOpener());
            var settings = new SettingsViewModel(main, exeDirectory: root);

            Assert.Equal("1 Plug-in gefunden.", settings.PluginsSummaryText);
            var plugin = Assert.Single(settings.DiscoveredPlugins);
            Assert.Equal("Bereit — noch nicht geladen", plugin.StatusText);
            Assert.Equal("Manifest gültig. Noch nicht geladen: Nur signierte Plug-ins werden geladen, und die Plug-in-Signierung ist noch nicht eingerichtet.", plugin.Reason);

            AddPlugin(root, "gpuz2", ValidManifestJson.Replace("com.example.gpuz", "com.example.gpuz2"));
            settings.RefreshPluginsCommand.Execute(null);
            Assert.Equal("2 Plug-ins gefunden.", settings.PluginsSummaryText);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_LanguageSwitch_ReDiscoversPluginsInGerman()
    {
        // Picking German after the page loaded re-runs discovery in
        // German: the list must not stay in the language it was first
        // discovered in. Manual setup (marker + plug-in before the
        // shell is built, English store): the marker makes the temp
        // dir the data folder, so the plug-ins folder under it is
        // hermetic.
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, AppDataRoot.PortableMarkerFileName), string.Empty);
        AddPlugin(root, "nodll", ValidManifestJson, withAssembly: false);

        try
        {
            var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
            Assert.True(store.Save(new AppSettings { LanguageCode = "en" }));
            var main = new MainViewModel(
                Array.Empty<ICollector>(),
                new UnavailableSensorMonitor(),
                new BuyerCheckEvaluator(),
                store,
                new ShellExportFileOpener());
            var settings = new SettingsViewModel(main, exeDirectory: root);
            Assert.Equal("Assembly missing", Assert.Single(settings.DiscoveredPlugins).StatusText);

            settings.LanguageCode = "de";

            var plugin = Assert.Single(settings.DiscoveredPlugins);
            Assert.Equal("Assembly fehlt", plugin.StatusText);
            Assert.Equal("Die Plug-in-Datei 'GpuZBridge.dll' fehlt in diesem Ordner.", plugin.Reason);
            Assert.Equal("1 Plug-in gefunden.", settings.PluginsSummaryText);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_LanguageSwitch_ReResolvesPortableToggleMessage()
    {
        // The toggle message is a Core string carried as a key, so a
        // language switch after the toggle re-resolves it without
        // toggling again. Manual setup: English store, marker present
        // so the toggle turns portable mode off.
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
            Assert.True(store.Save(new AppSettings { LanguageCode = "en" }));
            var main = new MainViewModel(
                Array.Empty<ICollector>(),
                new UnavailableSensorMonitor(),
                new BuyerCheckEvaluator(),
                store,
                new ShellExportFileOpener());
            var settings = new SettingsViewModel(main, exeDirectory: root);
            settings.TogglePortableModeCommand.Execute(null);
            Assert.Contains("Portable mode is off.", settings.PortableModeMessage, StringComparison.Ordinal);

            settings.LanguageCode = "de";

            Assert.Equal("Der tragbare Modus ist aus. Starten Sie TrueSpec neu: Von da an werden Daten im Standardordner gespeichert. Bereits neben der App gespeicherte Scans bleiben dort.", settings.PortableModeMessage);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    // Localisation, phase 2, French (v2.1.21): the whole Settings page
    // in French — the same second surface German phase 2 (v2.1.8)
    // translated. A shell seeded with French renders every card the
    // page owns; the per-item plug-in status/reason and the
    // portable-toggle message are Core diagnostic strings that take a
    // language code, so they render French too (the same mechanism
    // German phase 3, v2.1.9, added). One wording correction from the
    // phase-1 catalogue: the alert threshold label is "Seuil d'alerte
    // (°C)", the term Windows itself uses, not "Alerte au-dessus de".
    [Fact]
    public void Settings_FrenchSeed_RendersWholePageInFrench()
    {
        var (_, settings, root) = ViewModelWithTempStore(seedLanguage: "fr");

        try
        {
            Assert.Equal("PARAMÈTRES", settings.SettingsEyebrowText);
            Assert.Equal("Paramètres", settings.SettingsTitleText);
            Assert.Equal(
                "Des réglages simples et honnêtes. Chaque modification est enregistrée immédiatement et s'applique tout de suite. Rien n'est envoyé sur le réseau.",
                settings.SettingsIntroText);

            Assert.Equal("Apparence", settings.AppearanceTitleText);
            Assert.Equal("Le thème sombre est le thème par défaut. Le thème clair est disponible pour les pièces lumineuses.", settings.AppearanceNoteText);
            Assert.Equal("Sombre (par défaut)", settings.ThemeText);
            Assert.Equal("Sombre", settings.ThemeDarkText);
            Assert.Equal("Clair", settings.ThemeLightText);

            Assert.Equal("Fréquence d'actualisation des capteurs", settings.RefreshTitleText);
            Assert.Equal("La fréquence à laquelle la bande des capteurs est relue. Plus c'est rapide, plus c'est vivant ; plus c'est lent, plus le processeur est ménagé.", settings.RefreshNoteText);
            Assert.Equal(new[] { "1 seconde", "2 secondes", "5 secondes", "10 secondes", "30 secondes" },
                settings.RefreshRateOptions.Select(o => o.Text).ToList());
            Assert.Equal(new[] { 1, 2, 5, 10, 30 },
                settings.RefreshRateOptions.Select(o => o.Value).ToList());

            Assert.Equal("Journalisation des capteurs et alertes", settings.LoggingTitleText);
            Assert.Equal("Enregistrer les mesures des capteurs", settings.RecordTitleText);
            Assert.Equal("Activé", settings.ToggleOnText);
            Assert.Equal("Désactivé", settings.ToggleOffText);
            Assert.Equal("Alerte de température du processeur", settings.AlertTitleText);
            Assert.Equal("Aucune alerte de température définie.", settings.AlertSummaryText);
            Assert.Equal("Seuil d'alerte (°C)", settings.AlertAboveLabelText);
            Assert.Equal("Doit rester au-dessus pendant", settings.AlertDurationLabelText);
            Assert.Equal(new[] { "15 secondes", "30 secondes", "60 secondes", "120 secondes" },
                settings.ThresholdDurationOptions.Select(o => o.Text).ToList());

            Assert.Equal("Base de données matérielle", settings.HardwareDatabaseTitleText);
            Assert.Equal(
                "Affiche la fréquence boost nominale à partir d'une petite base de données intégrée des caractéristiques des processeurs et des cartes graphiques, lorsque votre processeur ou votre carte graphique y figure. La base de données est facultative et ne remplace jamais une valeur mesurée. Prend effet après redémarrage.",
                settings.HardwareDatabaseNoteText);
            Assert.StartsWith("Jeu de données v1.14.0 : ", settings.HardwareDatabaseVersionText, StringComparison.Ordinal);

            Assert.Equal("Dossier d'exportation des rapports", settings.ExportTitleText);
            Assert.EndsWith(" (par défaut)", settings.ExportFolderText, StringComparison.Ordinal);
            Assert.Equal("Choisir un dossier...", settings.ExportChooseText);
            Assert.Equal("Réinitialiser", settings.ExportResetText);

            Assert.Equal("Emplacement des données (mode portable)", settings.DataLocationTitleText);
            Assert.Equal("Le mode portable est désactivé. Les données sont conservées dans le dossier par défaut.", settings.PortableModeSummaryText);
            Assert.Equal("Activer le mode portable", settings.PortableModeActionText);

            Assert.Equal("Plug-ins", settings.PluginsTitleText);
            Assert.Equal("Aucun plug-in trouvé.", settings.PluginsSummaryText);
            Assert.Equal("Actualiser les plug-ins", settings.PluginsRefreshText);

            Assert.Equal("Confidentialité", settings.PrivacyTitleText);
            Assert.Contains("aucune télémétrie", settings.PrivacyText, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_PickingFrench_RerendersWholePageAndBack()
    {
        var (_, settings, root) = ViewModelWithTempStore();

        try
        {
            Assert.Equal("Settings", settings.SettingsTitleText);
            Assert.Equal("Dark (default)", settings.ThemeText);
            Assert.Contains("5 seconds", settings.RefreshRateOptions.Select(o => o.Text));

            settings.LanguageCode = "fr";

            Assert.Equal("Paramètres", settings.SettingsTitleText);
            Assert.Equal("Sombre (par défaut)", settings.ThemeText);
            Assert.Contains("5 secondes", settings.RefreshRateOptions.Select(o => o.Text));
            Assert.Equal("Aucune alerte de température définie.", settings.AlertSummaryText);
            Assert.Equal("Aucun plug-in trouvé.", settings.PluginsSummaryText);

            settings.LanguageCode = "en";

            Assert.Equal("Settings", settings.SettingsTitleText);
            Assert.Equal("Dark (default)", settings.ThemeText);
            Assert.Contains("5 seconds", settings.RefreshRateOptions.Select(o => o.Text));
            Assert.Equal("No temperature alert set.", settings.AlertSummaryText);
            Assert.Equal("No plug-ins found.", settings.PluginsSummaryText);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_French_AlertSummaryShowsNumbers()
    {
        var (_, settings, root) = ViewModelWithTempStore(seedLanguage: "fr");

        try
        {
            settings.CpuTemperatureThresholdText = "90";
            settings.CpuTemperatureThresholdSeconds = 30;
            settings.CpuTemperatureAlertEnabled = true;

            // Numbers keep the app's formatting inside the French
            // sentence: 90 stays "90", never "90,0".
            Assert.Equal(
                "Alerte lorsque la température du processeur atteint 90 °C pendant 30 secondes.",
                settings.AlertSummaryText);

            settings.CpuTemperatureThresholdText = "abc";
            Assert.Equal(
                "Saisissez une température entre 30 et 120 °C.",
                settings.ThresholdMessage);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_French_PluginsAreFrench()
    {
        // Mirror of Settings_German_PluginsAreGerman: the store is
        // seeded French, and the marker file makes the temp dir the
        // data folder, so the plug-ins folder under it is hermetic.
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, AppDataRoot.PortableMarkerFileName), string.Empty);
        AddPlugin(root, "gpuz", ValidManifestJson);

        try
        {
            var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
            Assert.True(store.Save(new AppSettings { LanguageCode = "fr" }));
            var main = new MainViewModel(
                Array.Empty<ICollector>(),
                new UnavailableSensorMonitor(),
                new BuyerCheckEvaluator(),
                store,
                new ShellExportFileOpener());
            var settings = new SettingsViewModel(main, exeDirectory: root);

            Assert.Equal("1 plug-in trouvé.", settings.PluginsSummaryText);
            var plugin = Assert.Single(settings.DiscoveredPlugins);
            Assert.Equal("Prêt — pas encore chargé", plugin.StatusText);
            Assert.Equal("Manifeste valide. Pas encore chargé : seuls les plug-ins signés sont chargés, et la signature des plug-ins n'est pas encore en place.", plugin.Reason);

            AddPlugin(root, "gpuz2", ValidManifestJson.Replace("com.example.gpuz", "com.example.gpuz2"));
            settings.RefreshPluginsCommand.Execute(null);
            Assert.Equal("2 plug-ins trouvés.", settings.PluginsSummaryText);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_LanguageSwitch_ReDiscoversPluginsInFrench()
    {
        // Mirror of Settings_LanguageSwitch_ReDiscoversPluginsInGerman:
        // picking French after the page loaded re-runs discovery in
        // French; the list must not stay in the language it was first
        // discovered in.
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, AppDataRoot.PortableMarkerFileName), string.Empty);
        AddPlugin(root, "nodll", ValidManifestJson, withAssembly: false);

        try
        {
            var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
            Assert.True(store.Save(new AppSettings { LanguageCode = "en" }));
            var main = new MainViewModel(
                Array.Empty<ICollector>(),
                new UnavailableSensorMonitor(),
                new BuyerCheckEvaluator(),
                store,
                new ShellExportFileOpener());
            var settings = new SettingsViewModel(main, exeDirectory: root);
            Assert.Equal("Assembly missing", Assert.Single(settings.DiscoveredPlugins).StatusText);

            settings.LanguageCode = "fr";

            var plugin = Assert.Single(settings.DiscoveredPlugins);
            Assert.Equal("Assembly manquant", plugin.StatusText);
            Assert.Equal("Le fichier de plug-in « GpuZBridge.dll » est absent de ce dossier.", plugin.Reason);
            Assert.Equal("1 plug-in trouvé.", settings.PluginsSummaryText);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_LanguageSwitch_ReResolvesPortableToggleMessageInFrench()
    {
        // Mirror of Settings_LanguageSwitch_ReResolvesPortableToggleMessage:
        // the toggle message is a Core string carried as a key, so a
        // language switch after the toggle re-resolves it without
        // toggling again. Manual setup: English store, marker present
        // so the toggle turns portable mode off.
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
            Assert.True(store.Save(new AppSettings { LanguageCode = "en" }));
            var main = new MainViewModel(
                Array.Empty<ICollector>(),
                new UnavailableSensorMonitor(),
                new BuyerCheckEvaluator(),
                store,
                new ShellExportFileOpener());
            var settings = new SettingsViewModel(main, exeDirectory: root);
            settings.TogglePortableModeCommand.Execute(null);
            Assert.Contains("Portable mode is off.", settings.PortableModeMessage, StringComparison.Ordinal);

            settings.LanguageCode = "fr";

            Assert.Equal("Le mode portable est désactivé. Redémarrez TrueSpec : à partir de là, les données sont conservées dans le dossier par défaut. Les scans déjà enregistrés à côté de l'application restent là.", settings.PortableModeMessage);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    // Localisation, phase 2, Spanish (v2.1.28): the whole Settings page
    // in Spanish — the same second surface German phase 2 (v2.1.8) and
    // French phase 2 (v2.1.21) translated. A shell seeded with Spanish
    // renders every card the page owns; the per-item plug-in
    // status/reason and the portable-toggle message are Core diagnostic
    // strings that take a language code, so they render Spanish too
    // (the same mechanism German phase 3, v2.1.9, added). One wording
    // correction from the phase-1 catalogue: the alert threshold label
    // is "Umbral de alerta (°C)", the term Windows itself uses, not
    // "Avisar por encima de".
    [Fact]
    public void Settings_SpanishSeed_RendersWholePageInSpanish()
    {
        var (_, settings, root) = ViewModelWithTempStore(seedLanguage: "es");

        try
        {
            Assert.Equal("CONFIGURACIÓN", settings.SettingsEyebrowText);
            Assert.Equal("Configuración", settings.SettingsTitleText);
            Assert.Equal(
                "Controles pequeños y honestos. Cada cambio se guarda de inmediato y se aplica al momento. Nada de esto llama a casa.",
                settings.SettingsIntroText);

            Assert.Equal("Apariencia", settings.AppearanceTitleText);
            Assert.Equal("El tema oscuro es el predeterminado. El tema claro está disponible para habitaciones con mucha luz.", settings.AppearanceNoteText);
            Assert.Equal("Oscuro (predeterminado)", settings.ThemeText);
            Assert.Equal("Oscuro", settings.ThemeDarkText);
            Assert.Equal("Claro", settings.ThemeLightText);

            Assert.Equal("Frecuencia de actualización de los sensores en vivo", settings.RefreshTitleText);
            Assert.Equal("Cada cuánto vuelve a leer la tira de sensores en vivo. Más rápido es más animado; más lento es más suave con la CPU.", settings.RefreshNoteText);
            Assert.Equal(new[] { "1 segundo", "2 segundos", "5 segundos", "10 segundos", "30 segundos" },
                settings.RefreshRateOptions.Select(o => o.Text).ToList());
            Assert.Equal(new[] { 1, 2, 5, 10, 30 },
                settings.RefreshRateOptions.Select(o => o.Value).ToList());

            Assert.Equal("Registro de sensores y alertas", settings.LoggingTitleText);
            Assert.Equal("Registrar lecturas de sensores", settings.RecordTitleText);
            Assert.Equal("Activado", settings.ToggleOnText);
            Assert.Equal("Desactivado", settings.ToggleOffText);
            Assert.Equal("Alerta de temperatura de la CPU", settings.AlertTitleText);
            Assert.Equal("No hay ninguna alerta de temperatura establecida.", settings.AlertSummaryText);
            Assert.Equal("Umbral de alerta (°C)", settings.AlertAboveLabelText);
            Assert.Equal("Debe mantenerse por encima durante", settings.AlertDurationLabelText);
            Assert.Equal(new[] { "15 segundos", "30 segundos", "60 segundos", "120 segundos" },
                settings.ThresholdDurationOptions.Select(o => o.Text).ToList());

            Assert.Equal("Base de datos de hardware", settings.HardwareDatabaseTitleText);
            Assert.Equal(
                "Muestra la frecuencia de boost nominal de una pequeña base de datos integrada de especificaciones de CPU y GPU cuando tu procesador o tarjeta gráfica aparece en la lista. La base de datos es opcional y nunca sustituye un valor medido. Se aplica después de reiniciar.",
                settings.HardwareDatabaseNoteText);
            Assert.StartsWith("Conjunto de datos v1.14.0: ", settings.HardwareDatabaseVersionText, StringComparison.Ordinal);

            Assert.Equal("Carpeta de exportación de informes", settings.ExportTitleText);
            Assert.EndsWith(" (predeterminada)", settings.ExportFolderText, StringComparison.Ordinal);
            Assert.Equal("Elegir carpeta...", settings.ExportChooseText);
            Assert.Equal("Restablecer valor predeterminado", settings.ExportResetText);

            Assert.Equal("Ubicación de los datos (modo portátil)", settings.DataLocationTitleText);
            Assert.Equal("El modo portátil está desactivado. Los datos se guardan en la carpeta predeterminada.", settings.PortableModeSummaryText);
            Assert.Equal("Activar el modo portátil", settings.PortableModeActionText);

            Assert.Equal("Complementos", settings.PluginsTitleText);
            Assert.Equal("No se encontraron complementos.", settings.PluginsSummaryText);
            Assert.Equal("Actualizar complementos", settings.PluginsRefreshText);

            Assert.Equal("Privacidad", settings.PrivacyTitleText);
            Assert.Contains("no tiene telemetría", settings.PrivacyText, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_PickingSpanish_RerendersWholePageAndBack()
    {
        var (_, settings, root) = ViewModelWithTempStore();

        try
        {
            Assert.Equal("Settings", settings.SettingsTitleText);
            Assert.Equal("Dark (default)", settings.ThemeText);
            Assert.Contains("5 seconds", settings.RefreshRateOptions.Select(o => o.Text));

            settings.LanguageCode = "es";

            Assert.Equal("Configuración", settings.SettingsTitleText);
            Assert.Equal("Oscuro (predeterminado)", settings.ThemeText);
            Assert.Contains("5 segundos", settings.RefreshRateOptions.Select(o => o.Text));
            Assert.Equal("No hay ninguna alerta de temperatura establecida.", settings.AlertSummaryText);
            Assert.Equal("No se encontraron complementos.", settings.PluginsSummaryText);

            settings.LanguageCode = "en";

            Assert.Equal("Settings", settings.SettingsTitleText);
            Assert.Equal("Dark (default)", settings.ThemeText);
            Assert.Contains("5 seconds", settings.RefreshRateOptions.Select(o => o.Text));
            Assert.Equal("No temperature alert set.", settings.AlertSummaryText);
            Assert.Equal("No plug-ins found.", settings.PluginsSummaryText);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_Spanish_AlertSummaryShowsNumbers()
    {
        var (_, settings, root) = ViewModelWithTempStore(seedLanguage: "es");

        try
        {
            settings.CpuTemperatureThresholdText = "90";
            settings.CpuTemperatureThresholdSeconds = 30;
            settings.CpuTemperatureAlertEnabled = true;

            // Numbers keep the app's formatting inside the Spanish
            // sentence: 90 stays "90", never "90,0".
            Assert.Equal(
                "Avisar cuando la temperatura de la CPU llegue a 90 °C durante 30 segundos.",
                settings.AlertSummaryText);

            settings.CpuTemperatureThresholdText = "abc";
            Assert.Equal(
                "Introduce una temperatura entre 30 y 120 °C.",
                settings.ThresholdMessage);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_Spanish_PluginsAreSpanish()
    {
        // Mirror of Settings_German_PluginsAreGerman: the store is
        // seeded Spanish, and the marker file makes the temp dir the
        // data folder, so the plug-ins folder under it is hermetic.
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, AppDataRoot.PortableMarkerFileName), string.Empty);
        AddPlugin(root, "gpuz", ValidManifestJson);

        try
        {
            var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
            Assert.True(store.Save(new AppSettings { LanguageCode = "es" }));
            var main = new MainViewModel(
                Array.Empty<ICollector>(),
                new UnavailableSensorMonitor(),
                new BuyerCheckEvaluator(),
                store,
                new ShellExportFileOpener());
            var settings = new SettingsViewModel(main, exeDirectory: root);

            Assert.Equal("Se encontró 1 complemento.", settings.PluginsSummaryText);
            var plugin = Assert.Single(settings.DiscoveredPlugins);
            Assert.Equal("Listo, aún no cargado", plugin.StatusText);
            Assert.Equal("Manifiesto válido. Aún no cargado: solo se cargan los complementos firmados, y la firma de complementos aún no está configurada.", plugin.Reason);

            AddPlugin(root, "gpuz2", ValidManifestJson.Replace("com.example.gpuz", "com.example.gpuz2"));
            settings.RefreshPluginsCommand.Execute(null);
            Assert.Equal("Se encontraron 2 complementos.", settings.PluginsSummaryText);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_LanguageSwitch_ReDiscoversPluginsInSpanish()
    {
        // Mirror of Settings_LanguageSwitch_ReDiscoversPluginsInGerman:
        // picking Spanish after the page loaded re-runs discovery in
        // Spanish; the list must not stay in the language it was first
        // discovered in.
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, AppDataRoot.PortableMarkerFileName), string.Empty);
        AddPlugin(root, "nodll", ValidManifestJson, withAssembly: false);

        try
        {
            var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
            Assert.True(store.Save(new AppSettings { LanguageCode = "en" }));
            var main = new MainViewModel(
                Array.Empty<ICollector>(),
                new UnavailableSensorMonitor(),
                new BuyerCheckEvaluator(),
                store,
                new ShellExportFileOpener());
            var settings = new SettingsViewModel(main, exeDirectory: root);
            Assert.Equal("Assembly missing", Assert.Single(settings.DiscoveredPlugins).StatusText);

            settings.LanguageCode = "es";

            var plugin = Assert.Single(settings.DiscoveredPlugins);
            Assert.Equal("Falta el ensamblado", plugin.StatusText);
            Assert.Equal("Falta el archivo de complemento 'GpuZBridge.dll' en esta carpeta.", plugin.Reason);
            Assert.Equal("Se encontró 1 complemento.", settings.PluginsSummaryText);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Settings_LanguageSwitch_ReResolvesPortableToggleMessageInSpanish()
    {
        // Mirror of Settings_LanguageSwitch_ReResolvesPortableToggleMessage:
        // the toggle message is a Core string carried as a key, so a
        // language switch after the toggle re-resolves it without
        // toggling again. Manual setup: English store, marker present
        // so the toggle turns portable mode off.
        var root = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
            Assert.True(store.Save(new AppSettings { LanguageCode = "en" }));
            var main = new MainViewModel(
                Array.Empty<ICollector>(),
                new UnavailableSensorMonitor(),
                new BuyerCheckEvaluator(),
                store,
                new ShellExportFileOpener());
            var settings = new SettingsViewModel(main, exeDirectory: root);
            settings.TogglePortableModeCommand.Execute(null);
            Assert.Contains("Portable mode is off.", settings.PortableModeMessage, StringComparison.Ordinal);

            settings.LanguageCode = "es";

            Assert.Equal("El modo portátil está desactivado. Reinicia TrueSpec: a partir de entonces, los datos se guardan en la carpeta predeterminada. Los análisis ya guardados junto a la aplicación permanecen allí.", settings.PortableModeMessage);
        }
        finally
        {
            DeleteTree(root);
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
