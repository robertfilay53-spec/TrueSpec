namespace TrueSpec.Core.HardwareDatabase;

using System.Globalization;
using System.Reflection;
using System.Text.Json;

/// <summary>
/// The bundled, optional hardware database (Beyond v2.0, item 5): a small,
/// versioned dataset of CPU model specifications, compiled by hand from the
/// manufacturers' public specification pages (Intel ARK, AMD product
/// pages). It answers exactly one question: "what is this CPU's rated
/// boost clock, according to its spec sheet?"
///
/// The hard rules, from the plan:
/// - SMALL and OPTIONAL. The dataset is tens of entries, not thousands,
///   and the user can turn it off in Settings (takes effect after a
///   restart, like the portable-mode toggle). A stale database is worse
///   than no database, so the version and update date travel with it and
///   are shown in Settings and in every spec's source.
/// - NEVER a measurement. A rated boost clock is a spec-sheet maximum,
///   not a reading: it is shown under its own label
///   (<see cref="Models.SpecLabels.RatedBoostClock"/>) with the dataset
///   version as its source, and it never replaces or overrides the
///   measured MaxClockSpeed. A model that matches nothing produces no
///   spec at all: the lookup never guesses.
/// - 100% local. The dataset is an embedded resource; the lookup reads
///   no file, makes no network call and needs no elevation.
/// </summary>
public interface IHardwareDatabase
{
    /// <summary>The dataset version, for example "1.0.0".</summary>
    string Version { get; }

    /// <summary>The date the dataset was last updated, ISO format (yyyy-MM-dd).</summary>
    string Updated { get; }

    /// <summary>How many CPU entries the dataset holds.</summary>
    int CpuCount { get; }

    /// <summary>
    /// The rated specification for a CPU model name (for example the
    /// Win32_Processor Name), or false when the model is not in the
    /// dataset. The name is normalized before matching; a name that
    /// matches nothing is not an error, it simply has no rated spec.
    /// </summary>
    bool TryGetCpu(string? modelName, out CpuSpec spec);
}

/// <summary>
/// The rated specification for one CPU model. All clock figures are
/// spec-sheet values from the manufacturer: Intel's is the ARK Max Turbo
/// Frequency (Performance-core), AMD's is the Max Boost Clock. The
/// E-core figure is Intel hybrid only; it is null for every other chip.
/// </summary>
public sealed record CpuSpec(
    string Model,
    string Vendor,
    string DisplayName,
    double BaseGhz,
    double BoostGhz,
    double? ECoreBoostGhz,
    string SourceUrl,
    string Retrieved);

public sealed class HardwareDatabase : IHardwareDatabase
{
    private const string EmbeddedResourceName = "TrueSpec.Core.HardwareDatabase.hardware-database.json";

    private static readonly Lazy<HardwareDatabase> DefaultLazy = new(LoadDefault);

    /// <summary>
    /// The bundled dataset. Loading never throws and never blocks the
    /// app: a missing or malformed embedded resource degrades to an
    /// empty database, which simply matches nothing.
    /// </summary>
    public static HardwareDatabase Default => DefaultLazy.Value;

    private readonly Dictionary<string, CpuSpec> _cpus;

    public string Version { get; }

    public string Updated { get; }

    public int CpuCount => _cpus.Count;

    private HardwareDatabase(string version, string updated, Dictionary<string, CpuSpec> cpus)
    {
        Version = version;
        Updated = updated;
        _cpus = cpus;
    }

    public bool TryGetCpu(string? modelName, out CpuSpec spec)
    {
        spec = null!;
        var key = NormalizeModel(modelName);
        if (key.Length == 0)
        {
            return false;
        }

        return _cpus.TryGetValue(key, out spec!);
    }

    /// <summary>
    /// Loads a database from JSON text in the bundled schema. Invalid
    /// entries are skipped, never fatal: a bad row must not take down
    /// the whole dataset, and a bad file degrades to an empty database.
    /// </summary>
    public static HardwareDatabase LoadFromJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Empty();
            }

            var version = GetString(root, "dataset_version");
            var updated = GetString(root, "updated");
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(updated))
            {
                return Empty();
            }

            var cpus = new Dictionary<string, CpuSpec>(StringComparer.Ordinal);
            if (root.TryGetProperty("cpus", out var cpusElement) && cpusElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in cpusElement.EnumerateArray())
                {
                    if (TryParseCpu(element) is { } spec && !cpus.ContainsKey(spec.Model))
                    {
                        cpus[spec.Model] = spec;
                    }
                }
            }

            return new HardwareDatabase(version, updated, cpus);
        }
        catch (JsonException)
        {
            return Empty();
        }
    }

    /// <summary>An empty database: version "0.0.0", no entries, matches nothing.</summary>
    public static HardwareDatabase Empty() => new("0.0.0", "0001-01-01", new Dictionary<string, CpuSpec>(StringComparer.Ordinal));

    private static HardwareDatabase LoadDefault()
    {
        try
        {
            using var stream = typeof(HardwareDatabase).Assembly.GetManifestResourceStream(EmbeddedResourceName);
            if (stream is null)
            {
                return Empty();
            }

            using var reader = new StreamReader(stream);
            return LoadFromJson(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            return Empty();
        }
    }

    private static CpuSpec? TryParseCpu(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var model = GetString(element, "model");
        var vendor = GetString(element, "vendor");
        var displayName = GetString(element, "display_name");
        var sourceUrl = GetString(element, "source_url");
        var retrieved = GetString(element, "retrieved");
        if (string.IsNullOrWhiteSpace(model)
            || string.IsNullOrWhiteSpace(vendor)
            || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(sourceUrl)
            || string.IsNullOrWhiteSpace(retrieved))
        {
            return null;
        }

        // The stored key must already be normalized: a dataset entry whose
        // key does not survive its own normalization is a data bug, and
        // shipping it would make the lookup silently miss. Skip it.
        if (!string.Equals(NormalizeModel(model), model, StringComparison.Ordinal))
        {
            return null;
        }

        if (!TryGetDouble(element, "base_ghz", out var baseGhz)
            || !TryGetDouble(element, "boost_ghz", out var boostGhz)
            || baseGhz <= 0
            || boostGhz < baseGhz)
        {
            return null;
        }

        double? eCoreBoost = null;
        if (element.TryGetProperty("e_core_boost_ghz", out var eCoreElement)
            && eCoreElement.ValueKind != JsonValueKind.Null)
        {
            if (!TryGetDouble(element, "e_core_boost_ghz", out var eCoreGhz)
                || eCoreGhz <= 0
                || eCoreGhz > boostGhz)
            {
                return null;
            }

            eCoreBoost = eCoreGhz;
        }

        return new CpuSpec(model, vendor, displayName, baseGhz, boostGhz, eCoreBoost, sourceUrl, retrieved);
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool TryGetDouble(JsonElement element, string property, out double value)
    {
        value = 0;
        return element.TryGetProperty(property, out var raw)
            && raw.ValueKind == JsonValueKind.Number
            && raw.TryGetDouble(out value)
            && double.IsFinite(value);
    }

    /// <summary>
    /// Normalizes a CPU model name to its lookup key. The same function
    /// normalizes both sides: the dataset's stored keys and the
    /// Win32_Processor Name at lookup time. Brand and trademark markers
    /// are stripped, so "Intel(R) Core(TM) i7-12700H" and "i7-12700H"
    /// are the same key; the family is kept, so an Intel i7 and an AMD
    /// Ryzen 7 can never collide ("I7-12700H" vs "RYZEN 7 5800H").
    /// A name that normalizes to nothing matches nothing.
    /// </summary>
    public static string NormalizeModel(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return string.Empty;
        }

        var normalized = modelName.ToUpperInvariant()
            .Replace("(R)", string.Empty, StringComparison.Ordinal)
            .Replace("(TM)", string.Empty, StringComparison.Ordinal)
            .Replace("®", string.Empty, StringComparison.Ordinal)
            .Replace("™", string.Empty, StringComparison.Ordinal)
            .Replace(" WITH RADEON GRAPHICS", string.Empty, StringComparison.Ordinal)
            .Replace(" PROCESSOR", string.Empty, StringComparison.Ordinal);

        // Collapse runs of whitespace to one space, then strip the brand
        // prefixes. The order matters: "INTEL CORE " before "INTEL ", so
        // an Ultra chip keeps its family ("ULTRA 7 155H").
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        normalized = string.Join(" ", parts);

        if (normalized.StartsWith("INTEL CORE ", StringComparison.Ordinal))
        {
            normalized = normalized["INTEL CORE ".Length..];
        }
        else if (normalized.StartsWith("INTEL ", StringComparison.Ordinal))
        {
            normalized = normalized["INTEL ".Length..];
        }
        else if (normalized.StartsWith("AMD RYZEN ", StringComparison.Ordinal))
        {
            normalized = "RYZEN " + normalized["AMD RYZEN ".Length..];
        }
        else if (normalized.StartsWith("AMD ", StringComparison.Ordinal))
        {
            normalized = normalized["AMD ".Length..];
        }

        return normalized.Trim();
    }
}
