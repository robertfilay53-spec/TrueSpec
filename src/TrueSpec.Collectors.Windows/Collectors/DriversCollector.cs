namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// Driver inventory from Win32_PnPSignedDriver, including real driver dates and
/// the age flags the buyer view uses.
/// </summary>
public sealed class DriversCollector : ICollector
{
    public string Id => "drivers";

    public string Name => "Drivers";

    public string Category => "Security";

    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(30);

    public bool RequiresAdministrator => false;

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var drivers = Wmi.Query(
            "root\\cimv2",
            "SELECT DeviceName, DriverDate, DriverVersion, DriverProviderName, IsSigned, InfName FROM Win32_PnPSignedDriver");

        if (drivers.Count == 0)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "CIM did not return Win32_PnPSignedDriver."));
        }

        var now = DateTimeOffset.UtcNow;
        var unsigned = drivers.Count(r => r.Bool("IsSigned") is false);
        var unknown = drivers.Count(r => r.Bool("IsSigned") is null);
        var dated = drivers
            .Select(r => new { Row = r, Date = r.Date("DriverDate") })
            .Where(x => x.Date is not null)
            .ToList();
        var old = dated.Count(x => (now - x.Date!.Value).TotalDays > 365d * 5d);
        var oldest = dated.OrderBy(x => x.Date!.Value).FirstOrDefault();

        // "All signed" is only true when every signature is known and
        // signed: drivers with an unknown signature state must not be
        // counted as signed.
        var headline = string.Create(
            CultureInfo.InvariantCulture,
            $"{drivers.Count} drivers - {(unsigned == 0 && unknown == 0 ? "all signed" : unsigned > 0 ? $"{unsigned} unsigned" : $"{unknown} with unknown signature")}");

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec("Installed drivers", drivers.Count.ToString(CultureInfo.InvariantCulture), "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec("Unsigned drivers", unsigned.ToString(CultureInfo.InvariantCulture), "CIM Win32_PnPSignedDriver.IsSigned"),
            CollectorSupport.Spec("Drivers older than 5 years", old.ToString(CultureInfo.InvariantCulture), "CIM Win32_PnPSignedDriver.DriverDate"),
            CollectorSupport.Spec("Oldest driver date", oldest?.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "CIM Win32_PnPSignedDriver.DriverDate"),
            CollectorSupport.Spec("Oldest driver device", oldest?.Row.String("DeviceName"), "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec("Very old driver flag", old switch
            {
                0 => "No",
                > 0 => "Yes",
                _ => CollectorSupport.NotReported,
            }, "CIM Win32_PnPSignedDriver.DriverDate older than 5 years"),
        };

        // Two drivers can share a DeviceName; duplicate spec labels
        // collapse in the comparison view, so number repeats.
        var usedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in dated
                     .Where(x => (now - x.Date!.Value).TotalDays > 365d * 5d)
                     .OrderBy(x => x.Date!.Value)
                     .Take(10))
        {
            var label = $"Old driver - {item.Row.String("DeviceName") ?? "device"}";
            var numbered = label;
            var n = 2;
            while (!usedLabels.Add(numbered))
            {
                numbered = $"{label} #{n++}";
            }

            specs.Add(CollectorSupport.Spec(
                numbered,
                string.Create(CultureInfo.InvariantCulture, $"{item.Date:yyyy-MM-dd} / {item.Row.String("DriverVersion") ?? "version not reported"}"),
                "CIM Win32_PnPSignedDriver"));
        }

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            headline,
            specs,
            "Drivers are the small programs that let Windows talk to hardware. All signed drivers is a good sign. A few very old drivers are normal, but many old drivers can cause instability."));
    }
}
