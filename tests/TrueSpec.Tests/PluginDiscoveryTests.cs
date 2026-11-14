using TrueSpec.Core.Collectors;
using TrueSpec.Core.Plugins;

namespace TrueSpec.Tests;

/// <summary>
/// Collector plug-ins, phase 1 (plan Beyond v2.0 item 2): discovery
/// lists plug-in folders and validates each manifest without loading
/// anything. Runs on both legs.
/// </summary>
public class PluginDiscoveryTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string ManifestJson(
        string id = "com.example.gpuz",
        string name = "GPU-Z bridge",
        int contractVersion = 1,
        string assembly = "GpuZBridge.dll") =>
        $$"""
        {
          "id": "{{id}}",
          "name": "{{name}}",
          "version": "1.0.0",
          "contractVersion": {{contractVersion}},
          "assembly": "{{assembly}}",
          "collectors": [
            { "id": "gpu-extra", "name": "GPU extra", "category": "GPU" }
          ]
        }
        """;

    private static string AddPlugin(string pluginsFolder, string folderName, string? manifestJson, bool withAssembly = true)
    {
        var folder = Path.Combine(pluginsFolder, folderName);
        Directory.CreateDirectory(folder);
        if (manifestJson is not null)
        {
            File.WriteAllText(Path.Combine(folder, PluginManifestParser.ManifestFileName), manifestJson);
        }

        if (withAssembly)
        {
            File.WriteAllText(Path.Combine(folder, "GpuZBridge.dll"), string.Empty);
        }

        return folder;
    }

    [Fact]
    public void MissingFolder_ReturnsEmptyWithoutError()
    {
        var root = TempDir();
        try
        {
            var result = PluginDiscovery.Discover(Path.Combine(root, "Plugins"));

            Assert.Empty(result.Plugins);
            Assert.Null(result.Error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EmptyFolder_ReturnsEmptyWithoutError()
    {
        var plugins = TempDir();
        try
        {
            var result = PluginDiscovery.Discover(plugins);

            Assert.Empty(result.Plugins);
            Assert.Null(result.Error);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void ValidPlugin_IsReadyWithDisplayName()
    {
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "gpuz", ManifestJson());

            var result = PluginDiscovery.Discover(plugins);

            var plugin = Assert.Single(result.Plugins);
            Assert.Equal(PluginStatus.Ready, plugin.Status);
            Assert.Equal("GPU-Z bridge", plugin.DisplayName);
            Assert.Equal("1.0.0", plugin.DisplayVersion);
            Assert.Equal("Ready — not loaded yet", plugin.StatusText);
            Assert.Contains("Not loaded yet", plugin.Reason, StringComparison.Ordinal);
            Assert.Null(result.Error);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void ContractMismatch_IsIncompatible()
    {
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "future", ManifestJson(contractVersion: CollectorContract.CurrentVersion + 1));

            var result = PluginDiscovery.Discover(plugins);

            var plugin = Assert.Single(result.Plugins);
            Assert.Equal(PluginStatus.IncompatibleContract, plugin.Status);
            Assert.Equal("Needs a newer TrueSpec", plugin.StatusText);
            Assert.Contains("contract version 2", plugin.Reason, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void MalformedManifest_IsInvalid()
    {
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "broken", "not json {{{");

            var result = PluginDiscovery.Discover(plugins);

            var plugin = Assert.Single(result.Plugins);
            Assert.Equal(PluginStatus.InvalidManifest, plugin.Status);
            Assert.Equal("Manifest problem", plugin.StatusText);
            Assert.Equal("broken", plugin.DisplayName);
            Assert.Equal("-", plugin.DisplayVersion);
            Assert.NotNull(plugin.Reason);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void FolderWithoutManifest_IsInvalid()
    {
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "nomanifest", manifestJson: null, withAssembly: false);

            var result = PluginDiscovery.Discover(plugins);

            var plugin = Assert.Single(result.Plugins);
            Assert.Equal(PluginStatus.InvalidManifest, plugin.Status);
            Assert.Contains("No plugin.json", plugin.Reason, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void MissingAssembly_IsMissingAssembly()
    {
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "nodll", ManifestJson(), withAssembly: false);

            var result = PluginDiscovery.Discover(plugins);

            var plugin = Assert.Single(result.Plugins);
            Assert.Equal(PluginStatus.MissingAssembly, plugin.Status);
            Assert.Equal("Assembly missing", plugin.StatusText);
            Assert.Contains("GpuZBridge.dll", plugin.Reason, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void MultiplePlugins_AreSortedByFolderName()
    {
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "zebra", ManifestJson(name: "Zebra"));
            AddPlugin(plugins, "alpha", ManifestJson(name: "Alpha"));
            AddPlugin(plugins, "mid", ManifestJson(name: "Mid"));

            var result = PluginDiscovery.Discover(plugins);

            Assert.Equal(new[] { "alpha", "mid", "zebra" }, result.Plugins.Select(p => p.FolderName));
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void NullFolder_ThrowsArgumentException()
    {
        // ThrowIfNullOrWhiteSpace throws the null subclass for null and
        // the plain type for blank: Assert.Throws matches the exact type.
        Assert.Throws<ArgumentNullException>(() => PluginDiscovery.Discover(null!));
        Assert.Throws<ArgumentException>(() => PluginDiscovery.Discover("   "));
    }

    [Fact]
    public void GermanLanguage_ReturnsGermanStatusAndReason()
    {
        // Localisation, phase 3 (v2.1.9): discovery takes a language
        // code and the per-item status and reason come back in it.
        // The default (no language) stays English, as the tests above
        // pin. The plug-in and file names inside the sentences stay
        // untranslated.
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "gpuz", ManifestJson());
            AddPlugin(plugins, "future", ManifestJson(contractVersion: CollectorContract.CurrentVersion + 1));
            AddPlugin(plugins, "nodll", ManifestJson(), withAssembly: false);
            AddPlugin(plugins, "nomanifest", manifestJson: null, withAssembly: false);

            var result = PluginDiscovery.Discover(plugins, "de");

            Assert.Equal(4, result.Plugins.Count);
            var ready = result.Plugins.Single(p => p.FolderName == "gpuz");
            Assert.Equal("Bereit — noch nicht geladen", ready.StatusText);
            Assert.Equal("Manifest gültig. Noch nicht geladen: Nur signierte Plug-ins werden geladen, und die Plug-in-Signierung ist noch nicht eingerichtet.", ready.Reason);
            var future = result.Plugins.Single(p => p.FolderName == "future");
            Assert.Equal("Braucht ein neueres TrueSpec", future.StatusText);
            Assert.Equal("Dieses Plug-in wurde für die Collector-Vertragsversion 2 geschrieben; dieses TrueSpec spricht Vertragsversion 1.", future.Reason);
            var nodll = result.Plugins.Single(p => p.FolderName == "nodll");
            Assert.Equal("Assembly fehlt", nodll.StatusText);
            Assert.Equal("Die Plug-in-Datei 'GpuZBridge.dll' fehlt in diesem Ordner.", nodll.Reason);
            var nomanifest = result.Plugins.Single(p => p.FolderName == "nomanifest");
            Assert.Equal("Manifestproblem", nomanifest.StatusText);
            Assert.Equal("Kein plugin.json-Manifest in diesem Ordner.", nomanifest.Reason);
            Assert.Null(result.Error);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void GermanLanguage_BadManifestReasonIsGerman()
    {
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "broken", "not json {{{");

            var result = PluginDiscovery.Discover(plugins, "de");

            var plugin = Assert.Single(result.Plugins);
            Assert.Equal(PluginStatus.InvalidManifest, plugin.Status);
            Assert.Equal("Manifestproblem", plugin.StatusText);
            Assert.Equal("Das Manifest ist kein gültiges JSON, daher wurde es übersprungen.", plugin.Reason);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void FrenchLanguage_ReturnsFrenchStatusAndReason()
    {
        // Localisation, phase 3, French (v2.1.22): discovery takes a
        // language code and the per-item status and reason come back in
        // it — mirror of GermanLanguage_ReturnsGermanStatusAndReason.
        // The default (no language) stays English, as the tests above
        // pin. The plug-in and file names inside the sentences stay
        // untranslated.
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "gpuz", ManifestJson());
            AddPlugin(plugins, "future", ManifestJson(contractVersion: CollectorContract.CurrentVersion + 1));
            AddPlugin(plugins, "nodll", ManifestJson(), withAssembly: false);
            AddPlugin(plugins, "nomanifest", manifestJson: null, withAssembly: false);

            var result = PluginDiscovery.Discover(plugins, "fr");

            Assert.Equal(4, result.Plugins.Count);
            var ready = result.Plugins.Single(p => p.FolderName == "gpuz");
            Assert.Equal("Prêt — pas encore chargé", ready.StatusText);
            Assert.Equal("Manifeste valide. Pas encore chargé : seuls les plug-ins signés sont chargés, et la signature des plug-ins n'est pas encore en place.", ready.Reason);
            var future = result.Plugins.Single(p => p.FolderName == "future");
            Assert.Equal("Nécessite un TrueSpec plus récent", future.StatusText);
            Assert.Equal("Ce plug-in a été écrit pour la version 2 du contrat de collecteur ; ce TrueSpec parle la version 1 du contrat.", future.Reason);
            var nodll = result.Plugins.Single(p => p.FolderName == "nodll");
            Assert.Equal("Assembly manquant", nodll.StatusText);
            Assert.Equal("Le fichier de plug-in « GpuZBridge.dll » est absent de ce dossier.", nodll.Reason);
            var nomanifest = result.Plugins.Single(p => p.FolderName == "nomanifest");
            Assert.Equal("Problème de manifeste", nomanifest.StatusText);
            Assert.Equal("Aucun manifeste plugin.json dans ce dossier.", nomanifest.Reason);
            Assert.Null(result.Error);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void FrenchLanguage_BadManifestReasonIsFrench()
    {
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "broken", "not json {{{");

            var result = PluginDiscovery.Discover(plugins, "fr");

            var plugin = Assert.Single(result.Plugins);
            Assert.Equal(PluginStatus.InvalidManifest, plugin.Status);
            Assert.Equal("Problème de manifeste", plugin.StatusText);
            Assert.Equal("Le manifeste n'est pas du JSON valide, il a donc été ignoré.", plugin.Reason);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void SpanishLanguage_ReturnsSpanishStatusAndReason()
    {
        // Localisation, phase 3, Spanish (v2.1.29): discovery takes a
        // language code and the per-item status and reason come back in
        // it — mirror of FrenchLanguage_ReturnsFrenchStatusAndReason.
        // The default (no language) stays English, as the tests above
        // pin. The plug-in and file names inside the sentences stay
        // untranslated. Values read from the real Spanish pack with
        // the scratch dump program (~/workspace/es-pack-dump/).
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "gpuz", ManifestJson());
            AddPlugin(plugins, "future", ManifestJson(contractVersion: CollectorContract.CurrentVersion + 1));
            AddPlugin(plugins, "nodll", ManifestJson(), withAssembly: false);
            AddPlugin(plugins, "nomanifest", manifestJson: null, withAssembly: false);

            var result = PluginDiscovery.Discover(plugins, "es");

            Assert.Equal(4, result.Plugins.Count);
            var ready = result.Plugins.Single(p => p.FolderName == "gpuz");
            Assert.Equal("Listo, aún no cargado", ready.StatusText);
            Assert.Equal("Manifiesto válido. Aún no cargado: solo se cargan los complementos firmados, y la firma de complementos aún no está configurada.", ready.Reason);
            var future = result.Plugins.Single(p => p.FolderName == "future");
            Assert.Equal("Necesita un TrueSpec más reciente", future.StatusText);
            Assert.Equal("Este complemento se escribió para la versión 2 del contrato de recopilador; este TrueSpec habla la versión 1 del contrato.", future.Reason);
            var nodll = result.Plugins.Single(p => p.FolderName == "nodll");
            Assert.Equal("Falta el ensamblado", nodll.StatusText);
            Assert.Equal("Falta el archivo de complemento 'GpuZBridge.dll' en esta carpeta.", nodll.Reason);
            var nomanifest = result.Plugins.Single(p => p.FolderName == "nomanifest");
            Assert.Equal("Problema con el manifiesto", nomanifest.StatusText);
            Assert.Equal("No hay ningún manifiesto plugin.json en esta carpeta.", nomanifest.Reason);
            Assert.Null(result.Error);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void SpanishLanguage_BadManifestReasonIsSpanish()
    {
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "broken", "not json {{{");

            var result = PluginDiscovery.Discover(plugins, "es");

            var plugin = Assert.Single(result.Plugins);
            Assert.Equal(PluginStatus.InvalidManifest, plugin.Status);
            Assert.Equal("Problema con el manifiesto", plugin.StatusText);
            Assert.Equal("El manifiesto no es JSON válido, así que se omitió.", plugin.Reason);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }

    [Fact]
    public void UnknownLanguage_FallsBackToEnglishStatusAndReason()
    {
        var plugins = TempDir();
        try
        {
            AddPlugin(plugins, "gpuz", ManifestJson());

            var result = PluginDiscovery.Discover(plugins, "xx");

            var plugin = Assert.Single(result.Plugins);
            Assert.Equal("Ready — not loaded yet", plugin.StatusText);
            Assert.Contains("Not loaded yet", plugin.Reason, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(plugins, recursive: true);
        }
    }
}
