namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using Microsoft.Win32;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// The U8 software audit: installed programs (name, version, install date,
/// publisher) from the Uninstall registry keys, Windows activation state
/// and licence channel from the Software Licensing CIM class, and the
/// installed .NET runtimes from the registry. Read-only throughout: every
/// registry key is opened without write access and nothing is changed.
/// No elevation is needed: the Uninstall keys are world-readable and the
/// licensing query works as a standard user.
/// <para>
/// A product key is never read, stored or displayed, ever: the licensing
/// query selects only ApplicationID, Description, LicenseStatus and Name.
/// The PartialProductKey property is never selected, and no code path
/// touches it.
/// </para>
/// <para>
/// Win32_Product is deliberately avoided: enumerating it triggers a repair
/// install of every MSI on the machine.
/// </para>
/// </summary>
public sealed class SoftwareCollector : ICollector
{
    public string Id => "software";

    public string Name => "Software";

    public string Category => "System";

    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(30);

    public bool RequiresAdministrator => false;

    // WMI query strings are hoisted so CollectorWmiSelectionTests can pin
    // the SELECT list to the properties this collector reads (hardening
    // item 7, F9 bug class). ApplicationID is the Windows product
    // (55c92734-d682-4d71-983e-d6ec3f16059f); filtering server-side keeps
    // Office and other licensed products out of the result.
    internal const string LicensingProductQuery =
        "SELECT ApplicationID, Description, LicenseStatus, Name FROM Win32_SoftwareLicensingProduct " +
        "WHERE ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f'";

    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var programs = new List<InstalledProgram>();
        var keysRead = new List<string>();
        var keysUnreadable = new List<string>();
        var systemComponentsSkipped = 0;

        foreach (var key in UninstallKeys())
        {
            cancellationToken.ThrowIfCancellationRequested();
            systemComponentsSkipped += ReadUninstallKey(key, programs, keysRead, keysUnreadable);
            if (programs.Count >= SoftwareAuditParser.MaxPrograms)
            {
                break;
            }
        }

        var license = ReadLicense(cancellationToken);
        var runtimes = ReadRuntimes();

        var truncated = programs.Count >= SoftwareAuditParser.MaxPrograms;
        if (keysRead.Count == 0 && license is null && runtimes.Count == 0)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "No Uninstall registry key, licensing data or runtime data could be read." +
                (keysUnreadable.Count == 0 ? string.Empty : $" Unreadable: {string.Join("; ", keysUnreadable)}.")));
        }

        return Task.FromResult(SoftwareAuditParser.BuildSnapshot(new SoftwareAuditData(
            programs,
            license,
            runtimes,
            keysRead,
            keysUnreadable,
            truncated,
            systemComponentsSkipped)));
    }

    private static IEnumerable<UninstallKey> UninstallKeys() => new[]
    {
        new UninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, "All users (64-bit)"),
        new UninstallKey(RegistryHive.LocalMachine, RegistryView.Registry32, "All users (32-bit)"),
        new UninstallKey(RegistryHive.CurrentUser, RegistryView.Default, "Current user"),
    };

    private static int ReadUninstallKey(
        UninstallKey key,
        List<InstalledProgram> programs,
        List<string> keysRead,
        List<string> keysUnreadable)
    {
        var skipped = 0;
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(key.Hive, key.View);
            using var uninstallKey = baseKey.OpenSubKey(UninstallPath, writable: false);
            if (uninstallKey is null)
            {
                // A missing Uninstall key is normal (nothing installed
                // for that scope): it counts as read, with zero entries.
                keysRead.Add($"{key.Location} (key absent)");
                return 0;
            }

            keysRead.Add(key.Location);
            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                if (programs.Count >= SoftwareAuditParser.MaxPrograms)
                {
                    return skipped;
                }

                try
                {
                    using var subKey = uninstallKey.OpenSubKey(subKeyName, writable: false);
                    if (subKey is null)
                    {
                        programs.Add(UnreadableEntry(subKeyName, key.Location));
                        continue;
                    }

                    // Windows components and updates carry SystemComponent=1:
                    // they are not user programs, so they are counted and
                    // skipped, not listed.
                    if (subKey.GetValue("SystemComponent") is int systemComponent && systemComponent == 1)
                    {
                        skipped++;
                        continue;
                    }

                    var program = SoftwareAuditParser.BuildProgram(
                        subKey.GetValue("DisplayName") as string,
                        subKey.GetValue("DisplayVersion") as string,
                        subKey.GetValue("Publisher") as string,
                        subKey.GetValue("InstallDate") as string,
                        key.Location);
                    programs.Add(program ?? UnreadableEntry(subKeyName, key.Location));
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException or ArgumentException)
                {
                    programs.Add(UnreadableEntry(subKeyName, key.Location));
                }
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException or ArgumentException)
        {
            keysUnreadable.Add($"{key.Location} ({exception.GetType().Name})");
        }

        return skipped;
    }

    private static InstalledProgram UnreadableEntry(string subKeyName, string location) =>
        new(
            string.Create(CultureInfo.InvariantCulture, $"Unreadable entry ({subKeyName})"),
            null,
            null,
            null,
            location,
            IsUnreadable: true);

    private static LicenseInfo? ReadLicense(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The 3-arg Query keeps the error: a failed query is partial data,
        // not a failure of the whole audit — the registry half still
        // reports, and the licence specs read as the honest no-reading
        // value. The query never selects PartialProductKey.
        var rows = Wmi.Query("root\\cimv2", LicensingProductQuery, out _);
        var row = rows.FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        return new LicenseInfo(
            SoftwareAuditParser.CleanText(row.String("Name")),
            row.UInt32("LicenseStatus"),
            SoftwareAuditParser.ParseLicenseChannel(row.String("Description")));
    }

    private static IReadOnlyList<InstalledRuntime> ReadRuntimes()
    {
        var runtimes = new List<InstalledRuntime>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string framework, string? version, string architecture)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return;
            }

            var clean = SoftwareAuditParser.CleanText(version);
            if (clean is null)
            {
                return;
            }

            if (seen.Add($"{framework}|{clean}|{architecture}"))
            {
                runtimes.Add(new InstalledRuntime(framework, clean, architecture));
            }
        }

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            var architecture = view == RegistryView.Registry64 ? "x64" : "x86";
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);

                // .NET Framework 4.x: the Release DWORD maps to a version
                // through the published table; a Version string, when
                // present, is reported as written.
                using (var full = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", writable: false))
                {
                    if (full is not null)
                    {
                        var release = full.GetValue("Release") switch
                        {
                            int i => (uint)i,
                            uint u => u,
                            long l when l >= 0 && l <= uint.MaxValue => (uint)l,
                            _ => (uint?)null,
                        };
                        Add(
                            SpecLabels.DotNetFramework,
                            release is null
                                ? full.GetValue("Version") as string
                                : SoftwareAuditParser.FrameworkReleaseToVersion(release.Value)
                                    ?? full.GetValue("Version") as string,
                            architecture);
                    }
                }

                // Modern .NET: each value name under the shared-framework
                // keys is an installed version, and sharedhost carries the
                // host version.
                using (var installed = baseKey.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions", writable: false))
                {
                    if (installed is not null)
                    {
                        using var arch = installed.OpenSubKey(architecture, writable: false);
                        if (arch is not null)
                        {
                            using var host = arch.OpenSubKey("sharedhost", writable: false);
                            Add("Microsoft .NET host", host?.GetValue("Version") as string, architecture);
                            using var sharedFx = arch.OpenSubKey("sharedfx", writable: false);
                            if (sharedFx is not null)
                            {
                                foreach (var framework in sharedFx.GetSubKeyNames())
                                {
                                    using var frameworkKey = sharedFx.OpenSubKey(framework, writable: false);
                                    if (frameworkKey is null)
                                    {
                                        continue;
                                    }

                                    foreach (var version in frameworkKey.GetValueNames())
                                    {
                                        Add(framework, version, architecture);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException or ArgumentException)
            {
                // A runtime key that cannot be read is not a failure: the
                // audit still reports the programs and the licence state,
                // and the runtime count simply covers what was readable.
            }
        }

        return runtimes;
    }

    private sealed record UninstallKey(RegistryHive Hive, RegistryView View, string Location);
}
