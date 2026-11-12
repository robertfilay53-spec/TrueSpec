namespace TrueSpec.Core.Collectors;

using System.Globalization;
using TrueSpec.Core.Models;

/// <summary>
/// One installed program, read from an Uninstall registry key. The version,
/// publisher and install date are whatever the installer wrote: any of them
/// may be absent, and an absent field is null, never a guess. An entry the
/// registry cannot decode at all (a subkey with no display name) is kept
/// with <see cref="IsUnreadable"/> set, so the snapshot lists it as
/// unreadable instead of silently dropping it.
/// </summary>
public sealed record InstalledProgram(
    string Name,
    string? Version,
    string? Publisher,
    DateTimeOffset? InstallDate,
    string Location,
    bool IsUnreadable = false)
{
    /// <summary>
    /// One display line: "version / publisher / installed yyyy-MM-dd".
    /// A missing part reads as the honest no-reading value, so the reader
    /// can tell a missing version from an empty one. An unreadable entry
    /// has no parts at all and reads as the no-reading value on its own.
    /// </summary>
    public string DisplayText
    {
        get
        {
            if (IsUnreadable)
            {
                return SpecLabels.NotReportedByHardware;
            }

            var version = string.IsNullOrWhiteSpace(Version) ? SpecLabels.NotReportedByHardware : Version.Trim();
            var publisher = string.IsNullOrWhiteSpace(Publisher) ? SpecLabels.NotReportedByHardware : Publisher.Trim();
            var installed = InstallDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? SpecLabels.NotReportedByHardware;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{version} / {publisher} / installed {installed}");
        }
    }
}

/// <summary>
/// One installed runtime: a framework name (".NET Framework",
/// "Microsoft .NETCore.App", "Microsoft.WindowsDesktop.App"), its version
/// as reported, and the architecture it was installed for ("x64", "x86").
/// </summary>
public sealed record InstalledRuntime(string Framework, string Version, string Architecture);

/// <summary>
/// Windows licence state from the Software Licensing CIM classes. The
/// product key is never part of this record: no collector reads it, stores
/// it or displays it, ever. <see cref="Channel"/> is Retail, OEM or Volume
/// when the licensing description names one, and null when it does not.
/// <see cref="LicenseStatus"/> is the raw
/// Win32_SoftwareLicensingProduct LicenseStatus code (1 means licensed);
/// a null status means CIM did not report one.
/// </summary>
public sealed record LicenseInfo(string? ProductName, uint? LicenseStatus, string? Channel);

/// <summary>
/// Everything the software audit snapshot is built from: the program list,
/// the licence state, the runtimes, and the read report (which registry
/// keys were read, which could not be read). The Windows collector only
/// fetches these inputs; this class decides what the user sees, so the
/// whole audit is unit-testable without a live registry or live CIM.
/// </summary>
public sealed record SoftwareAuditData(
    IReadOnlyList<InstalledProgram> Programs,
    LicenseInfo? License,
    IReadOnlyList<InstalledRuntime> Runtimes,
    IReadOnlyList<string> KeysRead,
    IReadOnlyList<string> KeysUnreadable,
    bool Truncated,
    int SystemComponentsSkipped);

/// <summary>
/// Pure parsing and snapshot building for the U8 software audit, kept in
/// Core so it is testable without a Windows registry or live CIM. The
/// registry reading itself lives in the Windows collector; this class only
/// interprets what was read.
/// </summary>
public static class SoftwareAuditParser
{
    /// <summary>
    /// A registry string can be arbitrarily long (a pasted blob in a
    /// publisher field). Values are capped at the same 512 characters as
    /// WMI spec values, with a visible "..." marker: truncation is shown,
    /// never silent.
    /// </summary>
    public const int MaxValueLength = 512;

    /// <summary>
    /// The program list is capped: an Uninstall key with hundreds of
    /// entries is either broken or hostile, and the detail page should
    /// not render all of them. The cap is reported, never silent.
    /// </summary>
    public const int MaxPrograms = 300;

    public static string? CleanText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        return text.Length <= MaxValueLength
            ? text
            : text[..(MaxValueLength - 3)] + "...";
    }

    /// <summary>
    /// Builds one entry from an Uninstall subkey. A blank display name is
    /// never a program: it returns null so the caller records the subkey
    /// as unreadable instead. Version, publisher and install date are
    /// cleaned but never invented when absent.
    /// </summary>
    public static InstalledProgram? BuildProgram(
        string? displayName,
        string? version,
        string? publisher,
        string? installDateText,
        string location)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        return new InstalledProgram(
            CleanText(displayName)!,
            CleanText(version),
            CleanText(publisher),
            ParseInstallDate(installDateText),
            location);
    }

    /// <summary>
    /// The Uninstall InstallDate format is "yyyyMMdd" (for example
    /// "20240115"). An ISO date is accepted too, because some installers
    /// write one. Anything else is not a date: it returns null instead
    /// of guessing, so the entry shows the honest no-reading value.
    /// </summary>
    public static DateTimeOffset? ParseInstallDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (DateTimeOffset.TryParseExact(
                trimmed,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var plain))
        {
            return plain;
        }

        return DateTimeOffset.TryParseExact(
            trimmed,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var iso)
            ? iso
            : null;
    }

    /// <summary>
    /// The licence channel, from the licensing product description, which
    /// reads like "Windows(R) Operating System, RETAIL channel". The
    /// match is case-insensitive and looks for the channel word itself,
    /// so a description that names no channel returns null instead of a
    /// guessed one.
    /// </summary>
    public static string? ParseLicenseChannel(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        if (description.Contains("RETAIL", StringComparison.OrdinalIgnoreCase))
        {
            return "Retail";
        }

        if (description.Contains("OEM", StringComparison.OrdinalIgnoreCase))
        {
            return "OEM";
        }

        if (description.Contains("VOLUME", StringComparison.OrdinalIgnoreCase))
        {
            return "Volume";
        }

        return null;
    }

    /// <summary>
    /// The documented Win32_SoftwareLicensingProduct LicenseStatus codes:
    /// 0 Unlicensed, 1 Licensed, 2 out-of-box grace period, 3
    /// out-of-tolerance grace period, 4 non-genuine grace period, 5
    /// notification, 6 extended grace period. An undocumented code is
    /// not a status: it returns null instead of a guessed word.
    /// </summary>
    public static string? LicenseStatusText(uint? status) => status switch
    {
        0 => "Not activated",
        1 => "Activated",
        2 => "Out-of-box grace period",
        3 => "Out-of-tolerance grace period",
        4 => "Non-genuine grace period",
        5 => "Notification",
        6 => "Extended grace period",
        _ => null,
    };

    /// <summary>
    /// The .NET Framework release number (the Release DWORD under
    /// NDP\v4\Full) mapped to its version, using Microsoft's published
    /// table. A release below the oldest known value is not a version:
    /// it returns null instead of guessing.
    /// </summary>
    public static string? FrameworkReleaseToVersion(uint release) => release switch
    {
        >= 533320 => "4.8.1",
        >= 528040 => "4.8",
        >= 461808 => "4.7.2",
        >= 461308 => "4.7.1",
        >= 460798 => "4.7",
        >= 394802 => "4.6.2",
        >= 394254 => "4.6.1",
        >= 393295 => "4.6",
        >= 379893 => "4.5.2",
        >= 378675 => "4.5.1",
        >= 378389 => "4.5",
        _ => null,
    };

    /// <summary>
    /// Builds the audit snapshot from already-read inputs. The headline
    /// names the program count and the activation state; when the WMI row
    /// cap truncated the list the headline says "300+" — never an exact
    /// count implying completeness — and a spec row states the list was
    /// truncated and the counts cover the first entries only. A missing
    /// licence state reads as the honest no-reading value, never as
    /// "activated" or "not activated".
    /// </summary>
    public static ComponentSnapshot BuildSnapshot(SoftwareAuditData data)
    {
        const string id = "software";
        const string name = "Software";
        const string category = "System";

        var unreadable = data.Programs.Count(p => p.IsUnreadable);
        var readable = data.Programs.Count - unreadable;
        var countText = data.Truncated
            ? $"{MaxPrograms}+"
            : readable.ToString(CultureInfo.InvariantCulture);
        var activation = LicenseStatusText(data.License?.LicenseStatus);
        var headline = data.Programs.Count == 0
            ? "No installed programs found"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{countText} program{(readable == 1 ? string.Empty : "s")} - {activation ?? SpecLabels.NotReportedByHardware}");

        var specs = new List<SpecValue>
        {
            new(
                SpecLabels.InstalledPrograms,
                readable.ToString(CultureInfo.InvariantCulture),
                "Registry Uninstall keys",
                NumericValue: readable,
                NumericUnit: "programs"),
            new(
                SpecLabels.WindowsActivation,
                activation ?? SpecLabels.NotReportedByHardware,
                "CIM Win32_SoftwareLicensingProduct"),
            new(
                SpecLabels.LicenseChannel,
                data.License?.Channel ?? SpecLabels.NotReportedByHardware,
                "CIM Win32_SoftwareLicensingProduct.Description"),
            new(
                SpecLabels.LicensedProduct,
                string.IsNullOrWhiteSpace(data.License?.ProductName)
                    ? SpecLabels.NotReportedByHardware
                    : CleanText(data.License.ProductName)!,
                "CIM Win32_SoftwareLicensingProduct.Name"),
            new(
                SpecLabels.InstalledRuntimes,
                data.Runtimes.Count.ToString(CultureInfo.InvariantCulture),
                "Registry .NET setup keys",
                NumericValue: data.Runtimes.Count,
                NumericUnit: "runtimes"),
            new(
                SpecLabels.KeysRead,
                string.Join("; ", data.KeysRead),
                "Registry Uninstall keys"),
        };

        if (unreadable > 0)
        {
            specs.Add(new(
                SpecLabels.UnreadableEntries,
                unreadable.ToString(CultureInfo.InvariantCulture),
                "Registry Uninstall keys",
                NumericValue: unreadable,
                NumericUnit: "entries"));
        }

        if (data.KeysUnreadable.Count > 0)
        {
            specs.Add(new(
                "Keys not readable",
                string.Join("; ", data.KeysUnreadable),
                "Registry access"));
        }

        if (data.SystemComponentsSkipped > 0)
        {
            specs.Add(new(
                "System components skipped",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{data.SystemComponentsSkipped} Windows component entries (SystemComponent) are not user programs and are not listed."),
                "Registry Uninstall keys"));
        }

        if (data.Truncated)
        {
            specs.Add(new(
                "Program list truncated",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Truncated at {MaxPrograms} entries; counts cover the first {MaxPrograms} programs only."),
                "SoftwareCollector display cap"));
        }

        // Two programs can share a display name; duplicate spec labels
        // collapse in the comparison view, so number repeats.
        var usedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var program in data.Programs)
        {
            var label = program.Name;
            var numbered = label;
            var n = 2;
            while (!usedLabels.Add(numbered))
            {
                numbered = $"{label} #{n++}";
            }

            specs.Add(new(numbered, program.DisplayText, program.Location));
        }

        foreach (var runtime in data.Runtimes)
        {
            var label = $"{runtime.Framework} ({runtime.Architecture})";
            var numbered = label;
            var n = 2;
            while (!usedLabels.Add(numbered))
            {
                numbered = $"{label} #{n++}";
            }

            specs.Add(new(numbered, runtime.Version, "Registry .NET setup keys"));
        }

        return ComponentSnapshot.NotMeasurable(
            id,
            name,
            category,
            id,
            headline,
            specs,
            "The software installed on this machine, whether Windows is genuinely activated, and which .NET runtimes are present. " +
            "An unactivated machine costs the buyer a licence, and an \"Office included\" claim is only real when Office is in this list. " +
            "TrueSpec never reads, stores or shows a product key: activation is reported as a state, never as a key.");
    }
}
