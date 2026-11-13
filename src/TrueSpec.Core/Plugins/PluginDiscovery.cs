namespace TrueSpec.Core.Plugins;

using TrueSpec.Core.Collectors;

/// <summary>
/// What discovery found for one plug-in folder. The status is about the
/// manifest and the files on disk only: no assembly is ever loaded here.
/// Loading is a later phase, gated on signed plug-ins (the signing story
/// is still being decided), so <see cref="PluginStatus.Ready"/> means
/// "valid and waiting", never "running".
/// </summary>
public enum PluginStatus
{
    /// <summary>Manifest valid, contract matches, assembly file present. Not loaded yet.</summary>
    Ready,

    /// <summary>Manifest valid, but written for a different contract version.</summary>
    IncompatibleContract,

    /// <summary>No manifest, or the manifest could not be parsed or validated.</summary>
    InvalidManifest,

    /// <summary>Manifest valid, but the named assembly file is missing from the folder.</summary>
    MissingAssembly,
}

/// <summary>
/// One plug-in folder as discovery sees it, pre-formatted for the UI.
/// <see cref="DisplayName"/> falls back to the folder name when there is
/// no manifest to name the plug-in; <see cref="DisplayVersion"/> is a
/// dash then. <see cref="StatusText"/> is the one-line status; <see cref="Reason"/>
/// is the plain-English detail behind it.
/// </summary>
public sealed record DiscoveredPlugin(
    string FolderName,
    string FolderPath,
    PluginManifest? Manifest,
    PluginStatus Status,
    string Reason)
{
    public string DisplayName => Manifest?.Name ?? FolderName;

    public string DisplayVersion => Manifest?.Version ?? "-";

    public string StatusText => Status switch
    {
        PluginStatus.Ready => "Ready — not loaded yet",
        PluginStatus.IncompatibleContract => Manifest is not null && Manifest.ContractVersion > CollectorContract.CurrentVersion
            ? "Needs a newer TrueSpec"
            : "Written for an older contract",
        PluginStatus.InvalidManifest => "Manifest problem",
        PluginStatus.MissingAssembly => "Assembly missing",
        _ => "Unknown",
    };
}

/// <summary>
/// The result of one discovery pass: the plug-ins found, in folder-name
/// order, plus an error when the folder itself could not be read or more
/// folders existed than the cap. A missing plug-ins folder is not an
/// error: it means no plug-ins are installed.
/// </summary>
public sealed record PluginDiscoveryResult(
    IReadOnlyList<DiscoveredPlugin> Plugins,
    string? Error);

/// <summary>
/// Collector plug-ins, phase 1 (plan Beyond v2.0 item 2): discovers
/// plug-in folders under the data root's Plugins folder and validates
/// each one's manifest, without loading anything. A folder is one
/// plug-in; its manifest is plugin.json. Discovery never throws: an
/// unreadable folder, a bad manifest, or a missing assembly is a status
/// with a plain-English reason, never an exception out of the pass.
/// Loading is deliberately NOT here: only signed plug-ins load, and the
/// signing story is still being decided, so a discovered plug-in is
/// listed and reported, never executed.
/// </summary>
public static class PluginDiscovery
{
    /// <summary>The most plug-in folders one pass lists.</summary>
    public const int MaxPlugins = 100;

    /// <summary>
    /// Discovers the plug-ins in a folder. A folder that does not exist
    /// means no plug-ins are installed: an empty list, no error.
    /// </summary>
    public static PluginDiscoveryResult Discover(string pluginsFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsFolder);

        if (!Directory.Exists(pluginsFolder))
        {
            return new PluginDiscoveryResult(Array.Empty<DiscoveredPlugin>(), null);
        }

        string[] folders;
        try
        {
            folders = Directory.GetDirectories(pluginsFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new PluginDiscoveryResult(
                Array.Empty<DiscoveredPlugin>(),
                "The plug-ins folder could not be read, so no plug-ins are listed.");
        }

        Array.Sort(folders, StringComparer.OrdinalIgnoreCase);
        string? error = null;
        if (folders.Length > MaxPlugins)
        {
            error = $"More than {MaxPlugins} plug-in folders were found; only the first {MaxPlugins} (alphabetically) are listed.";
            folders = folders[..MaxPlugins];
        }

        var plugins = new List<DiscoveredPlugin>(folders.Length);
        foreach (var folder in folders)
        {
            plugins.Add(DiscoverOne(folder));
        }

        return new PluginDiscoveryResult(plugins, error);
    }

    private static DiscoveredPlugin DiscoverOne(string folder)
    {
        var folderName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var manifestPath = Path.Combine(folder, PluginManifestParser.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return new DiscoveredPlugin(
                folderName,
                folder,
                null,
                PluginStatus.InvalidManifest,
                "No plugin.json manifest in this folder.");
        }

        PluginManifestResult parsed;
        try
        {
            using var stream = File.OpenRead(manifestPath);
            parsed = PluginManifestParser.Parse(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new DiscoveredPlugin(
                folderName,
                folder,
                null,
                PluginStatus.InvalidManifest,
                "The manifest could not be read.");
        }

        if (!parsed.Success || parsed.Manifest is null)
        {
            return new DiscoveredPlugin(
                folderName,
                folder,
                null,
                PluginStatus.InvalidManifest,
                parsed.Error ?? "The manifest could not be read.");
        }

        var manifest = parsed.Manifest;
        if (manifest.ContractVersion != CollectorContract.CurrentVersion)
        {
            return new DiscoveredPlugin(
                folderName,
                folder,
                manifest,
                PluginStatus.IncompatibleContract,
                $"This plug-in was written for collector contract version {manifest.ContractVersion}; this TrueSpec speaks contract version {CollectorContract.CurrentVersion}.");
        }

        if (!File.Exists(Path.Combine(folder, manifest.Assembly)))
        {
            return new DiscoveredPlugin(
                folderName,
                folder,
                manifest,
                PluginStatus.MissingAssembly,
                $"The plug-in file '{manifest.Assembly}' is missing from this folder.");
        }

        return new DiscoveredPlugin(
            folderName,
            folder,
            manifest,
            PluginStatus.Ready,
            "Manifest valid. Not loaded yet: only signed plug-ins load, and plug-in signing is not set up yet.");
    }
}
