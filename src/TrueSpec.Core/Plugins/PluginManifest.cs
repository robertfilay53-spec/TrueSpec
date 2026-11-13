namespace TrueSpec.Core.Plugins;

using System.Text;
using System.Text.Json;
using TrueSpec.Core.Collectors;

/// <summary>
/// One collector a plug-in provides, as declared in its manifest. The id
/// is the collector's stable machine id (the same shape as
/// <see cref="ICollector.Id"/>); the name and category are what the UI
/// shows and the scoring engine groups by.
/// </summary>
public sealed record PluginCollectorDeclaration(string Id, string Name, string Category);

/// <summary>
/// A plug-in manifest (plugin.json): the identity of one plug-in, the
/// collector contract version its collectors were written against, the
/// assembly file that carries them, and the collectors it provides.
/// The manifest is data, never code: it is validated before anything is
/// trusted, and it never causes an assembly to load. Loading is a later
/// phase, gated on signed plug-ins (see <see cref="PluginDiscovery"/>).
/// </summary>
public sealed record PluginManifest(
    string Id,
    string Name,
    string Version,
    int ContractVersion,
    string Assembly,
    IReadOnlyList<PluginCollectorDeclaration> Collectors);

/// <summary>
/// The result of parsing one manifest: the manifest, or a plain-English
/// reason it could not be used. A bad manifest is a failed parse, never
/// an exception out of the parse path.
/// </summary>
public sealed record PluginManifestResult(
    bool Success,
    PluginManifest? Manifest,
    string? Error)
{
    public static PluginManifestResult Ok(PluginManifest manifest) => new(true, manifest, null);

    public static PluginManifestResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// Collector plug-ins, phase 1 (plan Beyond v2.0 item 2): parses and
/// strictly validates a plugin.json manifest. Strict on purpose: a
/// manifest is a file the user (or a download) placed in the data folder,
/// so every field is checked before it is trusted. Unknown fields are
/// rejected, not ignored: a manifest written for a newer TrueSpec must
/// fail loudly instead of silently dropping a field it depends on. The
/// assembly field is a plain file name inside the plug-in's own folder:
/// a path, a parent-directory escape, or a non-.dll file is rejected, so
/// a manifest can never point the loader outside its folder. Files over
/// <see cref="MaxManifestBytes"/> are refused without being read.
/// </summary>
public static class PluginManifestParser
{
    /// <summary>
    /// The largest manifest accepted. A real manifest is a few hundred
    /// bytes; 64 KB leaves ample headroom while bounding what a
    /// hand-edited file can make the app read.
    /// </summary>
    public const int MaxManifestBytes = 64 * 1024;

    /// <summary>The manifest file name inside each plug-in folder.</summary>
    public const string ManifestFileName = "plugin.json";

    /// <summary>The most collectors one plug-in may declare.</summary>
    public const int MaxCollectors = 64;

    private const int MaxIdLength = 128;
    private const int MaxNameLength = 128;
    private const int MaxVersionLength = 64;

    private static readonly string[] TopLevelFields =
        { "id", "name", "version", "contractVersion", "assembly", "collectors" };

    private static readonly string[] CollectorFields = { "id", "name", "category" };

    /// <summary>
    /// Parses a manifest from a stream. Never throws: every failure is a
    /// <see cref="PluginManifestResult.Fail"/> with a plain-English
    /// reason.
    /// </summary>
    public static PluginManifestResult Parse(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            var bytes = ReadCapped(source);
            if (bytes is null)
            {
                return PluginManifestResult.Fail(
                    "The manifest is too large (over 64 KB) and was skipped.");
            }

            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return PluginManifestResult.Fail(
                    "The manifest is not a JSON object, so it was skipped.");
            }

            foreach (var property in root.EnumerateObject())
            {
                if (!TopLevelFields.Contains(property.Name, StringComparer.Ordinal))
                {
                    return PluginManifestResult.Fail(
                        $"The manifest has an unknown field '{property.Name}', so it was skipped.");
                }
            }

            var id = RequiredString(root, "id", MaxIdLength);
            if (id is null)
            {
                return PluginManifestResult.Fail(
                    "The manifest is missing its 'id', so it was skipped.");
            }

            var name = RequiredString(root, "name", MaxNameLength);
            if (name is null)
            {
                return PluginManifestResult.Fail(
                    "The manifest is missing its 'name', so it was skipped.");
            }

            var version = RequiredString(root, "version", MaxVersionLength);
            if (version is null)
            {
                return PluginManifestResult.Fail(
                    "The manifest is missing its 'version', so it was skipped.");
            }

            if (!root.TryGetProperty("contractVersion", out var contractVersionElement) ||
                contractVersionElement.ValueKind != JsonValueKind.Number ||
                !contractVersionElement.TryGetInt32(out var contractVersion))
            {
                return PluginManifestResult.Fail(
                    "The manifest is missing its 'contractVersion' (a whole number), so it was skipped.");
            }

            var assembly = RequiredString(root, "assembly", MaxIdLength);
            if (assembly is null)
            {
                return PluginManifestResult.Fail(
                    "The manifest is missing its 'assembly' file name, so it was skipped.");
            }

            var assemblyError = ValidateAssemblyFileName(assembly);
            if (assemblyError is not null)
            {
                return PluginManifestResult.Fail(assemblyError);
            }

            var collectors = ParseCollectors(root, out var collectorsError);
            if (collectors is null)
            {
                return PluginManifestResult.Fail(
                    collectorsError ?? "The manifest is missing its 'collectors' list, so it was skipped.");
            }

            if (collectors.Count == 0)
            {
                return PluginManifestResult.Fail(
                    "The manifest lists no collectors, so it was skipped.");
            }

            if (collectors.Count > MaxCollectors)
            {
                return PluginManifestResult.Fail(
                    $"The manifest lists {collectors.Count} collectors; at most {MaxCollectors} are allowed, so it was skipped.");
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var collector in collectors)
            {
                if (!IsCollectorId(collector.Id))
                {
                    return PluginManifestResult.Fail(
                        $"The collector id '{collector.Id}' is not a valid id (lowercase letters, digits and dashes, starting with a letter), so the manifest was skipped.");
                }

                if (!seenIds.Add(collector.Id))
                {
                    return PluginManifestResult.Fail(
                        $"The manifest lists the collector id '{collector.Id}' twice, so it was skipped.");
                }
            }

            return PluginManifestResult.Ok(new PluginManifest(
                id, name, version, contractVersion, assembly, collectors));
        }
        catch (JsonException)
        {
            return PluginManifestResult.Fail(
                "The manifest is not valid JSON, so it was skipped.");
        }
        catch (NotSupportedException)
        {
            return PluginManifestResult.Fail(
                "The manifest is not valid JSON, so it was skipped.");
        }
        catch (IOException)
        {
            return PluginManifestResult.Fail(
                "The manifest could not be read, so it was skipped.");
        }
        catch (InvalidOperationException)
        {
            return PluginManifestResult.Fail(
                "The manifest is not valid JSON, so it was skipped.");
        }
    }

    private static byte[]? ReadCapped(Stream source)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxManifestBytes)
            {
                return null;
            }
        }

        return buffer.ToArray();
    }

    private static string? RequiredString(JsonElement root, string field, int maxLength)
    {
        if (!root.TryGetProperty(field, out var element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            return null;
        }

        return value;
    }

    private static IReadOnlyList<PluginCollectorDeclaration>? ParseCollectors(JsonElement root, out string? error)
    {
        error = null;
        if (!root.TryGetProperty("collectors", out var element) ||
            element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var collectors = new List<PluginCollectorDeclaration>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                error = "A collector in the manifest is not an object, so the manifest was skipped.";
                return null;
            }

            foreach (var property in item.EnumerateObject())
            {
                if (!CollectorFields.Contains(property.Name, StringComparer.Ordinal))
                {
                    error = $"The collector has an unknown field '{property.Name}', so the manifest was skipped.";
                    return null;
                }
            }

            var id = RequiredString(item, "id", MaxIdLength);
            var name = RequiredString(item, "name", MaxNameLength);
            var category = RequiredString(item, "category", MaxNameLength);
            if (id is null || name is null || category is null)
            {
                error = "A collector in the manifest is missing its 'id', 'name' or 'category', so the manifest was skipped.";
                return null;
            }

            collectors.Add(new PluginCollectorDeclaration(id, name, category));
        }

        return collectors;
    }

    private static string? ValidateAssemblyFileName(string assembly)
    {
        if (assembly.IndexOfAny(new[] { '/', '\\' }) >= 0)
        {
            return "The manifest's 'assembly' must be a plain file name inside the plug-in's own folder, not a path, so it was skipped.";
        }

        if (assembly is "." or "..")
        {
            return "The manifest's 'assembly' must name a file, so it was skipped.";
        }

        if (assembly.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "The manifest's 'assembly' is not a valid file name, so it was skipped.";
        }

        if (!assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return "The manifest's 'assembly' must be a .dll file, so it was skipped.";
        }

        return null;
    }

    /// <summary>
    /// A collector id is lowercase letters, digits and dashes, starting
    /// with a letter: the same shape as the built-in collectors' ids
    /// ("storage", "crash"), so a plug-in collector can never collide
    /// with one by case alone.
    /// </summary>
    internal static bool IsCollectorId(string id)
    {
        if (string.IsNullOrEmpty(id) || !char.IsAsciiLetterLower(id[0]))
        {
            return false;
        }

        foreach (var c in id.AsSpan(1))
        {
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c != '-')
            {
                return false;
            }
        }

        return true;
    }
}
