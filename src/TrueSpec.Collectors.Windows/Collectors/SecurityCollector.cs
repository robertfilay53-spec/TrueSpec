namespace TrueSpec.Collectors.Windows.Collectors;

using Microsoft.Win32;
using TrueSpec.Core.Diagnostics;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// TPM, Secure Boot, BitLocker, virtualization-based security and driver
/// signatures from real Windows sources.
/// </summary>
public sealed class SecurityCollector : ICollector
{
    public string Id => "security";

    public string Name => "Security";

    public string Category => "Security";

    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(10);

    public bool RequiresAdministrator => false;

    // Hoisted so the selection can be pinned (F9 bug class): a property
    // read but not selected comes back null, so a security value the
    // machine does have would silently show as not reported. The TPM
    // query is one constant used for both namespaces (the secured
    // namespace first, the plain CIM namespace as the fallback).
    internal const string DeviceGuardQuery = "SELECT VirtualizationBasedSecurityStatus FROM Win32_DeviceGuard";
    internal const string AntiVirusProductQuery = "SELECT displayName, productState FROM AntiVirusProduct";
    internal const string FirewallProductQuery = "SELECT displayName, productState FROM FirewallProduct";
    internal const string TpmQuery = "SELECT SpecVersion, IsEnabled_InitialValue, IsActivated_InitialValue, IsOwned_InitialValue FROM Win32_Tpm";
    internal const string EncryptableVolumeQuery = "SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume";

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tpm = ReadTpm();
        var deviceGuard = Wmi.Query(
            "root\\Microsoft\\Windows\\DeviceGuard",
            DeviceGuardQuery).FirstOrDefault();
        var antivirus = Wmi.Query(
            "root\\SecurityCenter2",
            AntiVirusProductQuery);
        var firewall = Wmi.Query(
            "root\\SecurityCenter2",
            FirewallProductQuery);
        // Driver signatures come from the shared enumeration
        // (CollectorSupport): the same Win32_PnPSignedDriver list the
        // System collector counts, truncated honestly at the WMI row cap
        // instead of silently counted (H2 defect class).
        var driverSummary = CollectorSupport.ReadDriverSignatureSummary();

        var secureBoot = ReadSecureBoot();
        var bitLocker = ReadBitLocker();
        var uac = ReadUac();

        if (tpm is null && deviceGuard is null && driverSummary.Total is null or 0 && antivirus.Count == 0)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "Windows security CIM namespaces did not return TPM, Device Guard or driver signature data."));
        }

        var tpmVersion = tpm is { } tpmValue ? tpmValue.Version : null;
        var tpmEnabled = tpm is { } tpmEnabledValue ? tpmEnabledValue.Enabled : null;
        var tpmActivated = tpm is { } tpmActivatedValue ? tpmActivatedValue.Activated : null;
        var tpmOwned = tpm is { } tpmOwnedValue ? tpmOwnedValue.Owned : null;

        var headline = string.Join(" - ", new[]
        {
            tpmVersion is null ? null : $"TPM {tpmVersion}",
            secureBoot == "On" ? "Secure Boot on" : null,
        }.Where(v => !string.IsNullOrWhiteSpace(v)));

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(SpecLabels.TPMVersion, tpmVersion, "CIM Win32_Tpm"),
            CollectorSupport.Spec(SpecLabels.TPMEnabled, tpmEnabled, "CIM Win32_Tpm"),
            CollectorSupport.Spec(SpecLabels.TPMActivated, tpmActivated, "CIM Win32_Tpm"),
            CollectorSupport.Spec(SpecLabels.TPMOwned, tpmOwned, "CIM Win32_Tpm"),
            CollectorSupport.Spec(SpecLabels.SecureBoot, secureBoot, "Windows registry SecureBoot\\State"),
            CollectorSupport.Spec(SpecLabels.BitLocker, bitLocker, "CIM Win32_EncryptableVolume"),
            CollectorSupport.Spec(SpecLabels.VirtualizationBasedSecurity, DeviceGuardText(deviceGuard), "CIM Win32_DeviceGuard"),
            CollectorSupport.Spec(SpecLabels.UserAccountControl, uac, "Windows registry EnableLUA"),
            CollectorSupport.Spec(SpecLabels.Antivirus, antivirus.Count == 0
                ? null
                : string.Join(", ", antivirus.Select(r => r.String("displayName")).Where(v => !string.IsNullOrWhiteSpace(v))), "CIM SecurityCenter2 AntiVirusProduct"),
            CollectorSupport.Spec(SpecLabels.Firewall, firewall.Count == 0
                ? null
                : string.Join(", ", firewall.Select(r => r.String("displayName")).Where(v => !string.IsNullOrWhiteSpace(v))), "CIM SecurityCenter2 FirewallProduct"),
        };

        // The unsigned-drivers spec is built by a pure helper (same
        // pattern as DriversCollector.BuildSnapshot, PR #12) so the
        // truncation handling is unit-testable without live WMI. It is
        // the last spec in the list.
        specs.AddRange(DriverSignatureSpecs(driverSummary));

        foreach (var item in antivirus)
        {
            specs.Add(CollectorSupport.Spec(
                $"Antivirus state - {item.String("displayName") ?? "product"}",
                DecodeProductState(item.UInt32("productState")),
                "CIM SecurityCenter2 AntiVirusProduct.productState"));
        }

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            string.IsNullOrWhiteSpace(headline) ? "Security" : headline,
            specs,
            "This block shows the security features that protect the machine. There is no health percentage here because security state is not a wear measurement. A used laptop with BitLocker and Secure Boot already on has been set up properly."));
    }

    /// <summary>
    /// Pure unsigned-drivers spec, so the truncation handling is
    /// unit-testable without live WMI (same pattern as
    /// DriversCollector.BuildSnapshot, PR #12). The count is the rows
    /// actually read, qualified by the truncation note when the list
    /// was truncated: an unsigned driver may sit beyond the first
    /// 1000 rows, so the number is a lower bound, never a total.
    /// </summary>
    internal static List<SpecValue> DriverSignatureSpecs(CollectorSupport.DriverSignatureSummary summary)
    {
        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(
                SpecLabels.UnsignedDrivers,
                summary.Total is null or 0 ? null : summary.Unsigned?.ToString(),
                "CIM Win32_PnPSignedDriver.IsSigned"),
        };

        if (summary.Truncated)
        {
            specs.Add(CollectorSupport.DriverListTruncationNote());
        }

        return specs;
    }

    private static (string? Version, string? Enabled, string? Activated, string? Owned)? ReadTpm()
    {
        var rows = Wmi.Query("root\\CIMV2\\Security\\MicrosoftTpm", TpmQuery);
        if (rows.Count == 0)
        {
            rows = Wmi.Query("root\\cimv2", TpmQuery);
        }

        var tpm = rows.FirstOrDefault();
        if (tpm is null)
        {
            return null;
        }

        var version = tpm.String("SpecVersion")?.Split(',')[0].Trim();
        return (
            version,
            YesNo(tpm.Bool("IsEnabled_InitialValue")),
            YesNo(tpm.Bool("IsActivated_InitialValue")),
            YesNo(tpm.Bool("IsOwned_InitialValue")));
    }

    private static string DeviceGuardText(WmiRow? deviceGuard)
    {
        if (deviceGuard is null)
        {
            return SpecLabels.NotReportedByHardware;
        }

        return deviceGuard.UInt32("VirtualizationBasedSecurityStatus") switch
        {
            0 => "Off",
            1 => "Enabled, not running",
            2 => "Running",
            _ => SpecLabels.NotReportedByHardware,
        };
    }

    private static string ReadSecureBoot()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            var value = key?.GetValue("UEFISecureBootEnabled");
            return value switch
            {
                int number => number == 1 ? "On" : "Off",
                _ => SpecLabels.NotReportedByHardware,
            };
        }
        catch
        {
            return SpecLabels.NotReportedByHardware;
        }
    }

    private static string ReadBitLocker()
    {
        try
        {
            var rows = Wmi.Query(
                "root\\CIMV2\\Security\\MicrosoftVolumeEncryption",
                EncryptableVolumeQuery);
            if (rows.Count == 0)
            {
                return SpecLabels.NotReportedByHardware;
            }

            var systemVolume = rows.FirstOrDefault(r =>
                string.Equals(r.String("DriveLetter"), "C:", StringComparison.OrdinalIgnoreCase))
                ?? rows[0];

            return systemVolume.UInt32("ProtectionStatus") switch
            {
                0 => "Off",
                1 => "On",
                2 => "Unknown",
                _ => SpecLabels.NotReportedByHardware,
            };
        }
        catch
        {
            return SpecLabels.NotReportedByHardware;
        }
    }

    private static string ReadUac()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            var value = key?.GetValue("EnableLUA");
            return value switch
            {
                int number => number == 1 ? "On" : "Off",
                _ => SpecLabels.NotReportedByHardware,
            };
        }
        catch
        {
            return SpecLabels.NotReportedByHardware;
        }
    }

    private static string? YesNo(bool? value) => value switch
    {
        true => "Yes",
        false => "No",
        _ => null,
    };

    // The WSC productState bitmask is decoded in Core
    // (TrueSpec.Core.Diagnostics.WindowsSecurityProductState) so the
    // bit layout is covered by tests: bits 12-15 are the product state
    // (0 off, 1 on, 2 snoozed) and bits 4-7 are the definition status
    // (0 up to date, 1 out of date).
    private static string DecodeProductState(uint? state) =>
        WindowsSecurityProductState.DecodeProductState(state);
}
