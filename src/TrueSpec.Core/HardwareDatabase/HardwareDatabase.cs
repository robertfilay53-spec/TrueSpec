namespace TrueSpec.Core.HardwareDatabase;

using System.Globalization;
using System.Reflection;
using System.Text.Json;

/// <summary>
/// The bundled, optional hardware database (Beyond v2.0, item 5): a small,
/// versioned dataset of CPU and GPU model specifications, compiled by hand
/// from the manufacturers' public specification pages (Intel ARK, AMD
/// product pages, NVIDIA GeForce pages). It answers exactly one question
/// per component: "what is this model's rated boost clock, according to
/// its spec sheet?" (plus, for GPUs, its rated memory and board power).
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
///   measured MaxClockSpeed (CPU) or the live GPU clock and measured
///   dedicated memory (GPU). A model that matches nothing produces no
///   spec at all: the lookup never guesses.
/// - 100% local. The dataset is an embedded resource; the lookup reads
///   no file, makes no network call and needs no elevation.
/// </summary>
public interface IHardwareDatabase
{
    /// <summary>The dataset version, for example "1.1.0".</summary>
    string Version { get; }

    /// <summary>The date the dataset was last updated, ISO format (yyyy-MM-dd).</summary>
    string Updated { get; }

    /// <summary>How many CPU entries the dataset holds.</summary>
    int CpuCount { get; }

    /// <summary>How many GPU entries the dataset holds.</summary>
    int GpuCount { get; }

    /// <summary>
    /// The rated specification for a CPU model name (for example the
    /// Win32_Processor Name), or false when the model is not in the
    /// dataset. The name is normalized before matching; a name that
    /// matches nothing is not an error, it simply has no rated spec.
    /// </summary>
    bool TryGetCpu(string? modelName, out CpuSpec spec);

    /// <summary>
    /// The rated specification for a GPU model name (for example the
    /// Win32_VideoController Name), or false when the model is not in the
    /// dataset. The name is normalized before matching; a name that
    /// matches nothing is not an error, it simply has no rated spec.
    /// </summary>
    bool TryGetGpu(string? modelName, out GpuSpec spec);
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

/// <summary>
/// The rated specification for one GPU model. All figures are spec-sheet
/// values from the manufacturer: NVIDIA's Boost Clock, AMD's Boost
/// Frequency (up to); the memory is the card's VRAM size; the board
/// power is NVIDIA's Total Board Power / Total Graphics Power / TDP,
/// AMD's Typical Board Power (Desktop). For laptop GPUs the board power
/// is the MAXIMUM of the manufacturer-published TGP range, and the range
/// is recorded in <see cref="TdpNote"/>; a laptop card's power varies
/// by laptop, so a single figure without the range would mislead.
/// The boost clock is null when the Windows device name covers two
/// variants with different boost clocks (GTX 1650 G5/G6): the entry then
/// shows only its rated memory and board power. The memory is null when
/// the Windows device name cannot distinguish memory variants of one
/// model (for example the RTX 4060 Ti 8 GB and 16 GB both report as
/// "NVIDIA GeForce RTX 4060 Ti"): the boost clock and board power are
/// the same for both variants, so they are shown, but no memory figure
/// is guessed.
/// </summary>
public sealed record GpuSpec(
    string Model,
    string Vendor,
    string DisplayName,
    double? BoostGhz,
    int? MemoryGb,
    int TdpW,
    string? TdpNote,
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
    private readonly Dictionary<string, GpuSpec> _gpus;

    public string Version { get; }

    public string Updated { get; }

    public int CpuCount => _cpus.Count;

    public int GpuCount => _gpus.Count;

    private HardwareDatabase(string version, string updated, Dictionary<string, CpuSpec> cpus, Dictionary<string, GpuSpec> gpus)
    {
        Version = version;
        Updated = updated;
        _cpus = cpus;
        _gpus = gpus;
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

    public bool TryGetGpu(string? modelName, out GpuSpec spec)
    {
        spec = null!;
        var key = NormalizeGpuModel(modelName);
        if (key.Length == 0)
        {
            return false;
        }

        return _gpus.TryGetValue(key, out spec!);
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

            var gpus = new Dictionary<string, GpuSpec>(StringComparer.Ordinal);
            if (root.TryGetProperty("gpus", out var gpusElement) && gpusElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in gpusElement.EnumerateArray())
                {
                    if (TryParseGpu(element) is { } gpuSpec && !gpus.ContainsKey(gpuSpec.Model))
                    {
                        gpus[gpuSpec.Model] = gpuSpec;
                    }
                }
            }

            return new HardwareDatabase(version, updated, cpus, gpus);
        }
        catch (JsonException)
        {
            return Empty();
        }
    }

    /// <summary>An empty database: version "0.0.0", no entries, matches nothing.</summary>
    public static HardwareDatabase Empty() => new("0.0.0", "0001-01-01", new Dictionary<string, CpuSpec>(StringComparer.Ordinal), new Dictionary<string, GpuSpec>(StringComparer.Ordinal));

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

    private static GpuSpec? TryParseGpu(JsonElement element)
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
        if (!string.Equals(NormalizeGpuModel(model), model, StringComparison.Ordinal))
        {
            return null;
        }

        if (!TryGetInt(element, "tdp_w", out var tdpW)
            || tdpW <= 0)
        {
            return null;
        }

        // The boost clock is optional: a model whose Windows device name
        // covers two variants with different boost clocks (GTX 1650
        // G5/G6) carries no boost figure rather than a guessed one.
        // When present it must be a positive number of gigahertz.
        double? boostGhz = null;
        if (element.TryGetProperty("boost_ghz", out var boostElement)
            && boostElement.ValueKind != JsonValueKind.Null)
        {
            if (!TryGetDouble(element, "boost_ghz", out var boost)
                || boost <= 0)
            {
                return null;
            }

            boostGhz = boost;
        }

        // The memory is optional: a model whose Windows device name cannot
        // distinguish memory variants (RTX 4060 Ti 8 GB vs 16 GB) carries
        // no memory figure rather than a guessed one. When present it must
        // be a positive whole number of gigabytes.
        int? memoryGb = null;
        if (element.TryGetProperty("memory_gb", out var memoryElement)
            && memoryElement.ValueKind != JsonValueKind.Null)
        {
            if (!TryGetInt(element, "memory_gb", out var memory)
                || memory <= 0)
            {
                return null;
            }

            memoryGb = memory;
        }

        string? tdpNote = null;
        if (element.TryGetProperty("tdp_note", out var noteElement)
            && noteElement.ValueKind == JsonValueKind.String)
        {
            var note = noteElement.GetString();
            if (!string.IsNullOrWhiteSpace(note))
            {
                tdpNote = note;
            }
        }

        return new GpuSpec(model, vendor, displayName, boostGhz, memoryGb, tdpW, tdpNote, sourceUrl, retrieved);
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

    private static bool TryGetInt(JsonElement element, string property, out int value)
    {
        value = 0;
        return element.TryGetProperty(property, out var raw)
            && raw.ValueKind == JsonValueKind.Number
            && raw.TryGetInt32(out value);
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

    /// <summary>
    /// Normalizes a GPU model name to its lookup key. The same function
    /// normalizes both sides: the dataset's stored keys and the
    /// Win32_VideoController Name at lookup time. Brand and trademark
    /// markers are stripped, so "NVIDIA GeForce RTX 4060 Laptop GPU" and
    /// "RTX 4060 Laptop" are the same key; the family and the variant
    /// are kept, so a laptop card and its desktop namesake can never
    /// collide ("RTX 4060 LAPTOP" vs "RTX 4060"), and neither can the
    /// memory variants ("GTX 1060 6GB" vs "GTX 1060 3GB").
    /// A name that normalizes to nothing matches nothing.
    /// </summary>
    public static string NormalizeGpuModel(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return string.Empty;
        }

        var normalized = modelName.ToUpperInvariant()
            .Replace("(R)", string.Empty, StringComparison.Ordinal)
            .Replace("(TM)", string.Empty, StringComparison.Ordinal)
            .Replace("®", string.Empty, StringComparison.Ordinal)
            .Replace("™", string.Empty, StringComparison.Ordinal);

        // Collapse runs of whitespace to one space, then strip the brand
        // prefixes. The order matters: "NVIDIA GEFORCE " before
        // "NVIDIA ", so the family token survives. "INTEL " is stripped
        // but "ARC" is kept, so an Arc card keeps its family ("ARC A750").
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        normalized = string.Join(" ", parts);

        if (normalized.StartsWith("NVIDIA GEFORCE ", StringComparison.Ordinal))
        {
            normalized = normalized["NVIDIA GEFORCE ".Length..];
        }
        else if (normalized.StartsWith("NVIDIA ", StringComparison.Ordinal))
        {
            normalized = normalized["NVIDIA ".Length..];
        }
        else if (normalized.StartsWith("GEFORCE ", StringComparison.Ordinal))
        {
            normalized = normalized["GEFORCE ".Length..];
        }
        else if (normalized.StartsWith("AMD RADEON ", StringComparison.Ordinal))
        {
            normalized = normalized["AMD RADEON ".Length..];
        }
        else if (normalized.StartsWith("AMD ", StringComparison.Ordinal))
        {
            normalized = normalized["AMD ".Length..];
        }
        else if (normalized.StartsWith("RADEON ", StringComparison.Ordinal))
        {
            normalized = normalized["RADEON ".Length..];
        }
        else if (normalized.StartsWith("INTEL ", StringComparison.Ordinal))
        {
            normalized = normalized["INTEL ".Length..];
        }

        if (normalized.EndsWith(" GPU", StringComparison.Ordinal))
        {
            normalized = normalized[..^" GPU".Length];
        }
        else if (normalized.EndsWith(" GRAPHICS", StringComparison.Ordinal))
        {
            normalized = normalized[..^" GRAPHICS".Length];
        }

        return normalized.Trim();
    }
}
