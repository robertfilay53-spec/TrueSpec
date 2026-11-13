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

    // WMI query strings are hoisted so CollectorWmiSelectionTests can pin
    // the SELECT list to the properties this collector reads (hardening
    // item 7, F9 bug class).
    internal const string SignedDriverQuery = "SELECT DeviceName, DriverDate, DriverVersion, IsSigned FROM Win32_PnPSignedDriver";

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Only DeviceName, DriverDate, DriverVersion and IsSigned are read
        // below. DriverProviderName and InfName were fetched for every one
        // of the hundreds of driver rows and thrown away; Win32_PnPSignedDriver
        // is enumerated by three collectors, so every dropped column counts.
        // The 3-arg Query keeps the error: the class is capped at
        // Wmi.MaxRows (1000), and a truncated list must never be presented
        // as complete (see BuildSnapshot).
        var drivers = Wmi.Query(
            "root\\cimv2",
            SignedDriverQuery,
            out var queryError);

        if (drivers.Count == 0)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "CIM did not return Win32_PnPSignedDriver."));
        }

        return Task.FromResult(BuildSnapshot(drivers, queryError));
    }

    /// <summary>
    /// Pure snapshot builder, so the truncation handling is unit-testable
    /// without live WMI: the collector only fetches rows, this decides
    /// what the user sees. When the WMI row cap truncated the list
    /// (<paramref name="queryError"/> is <see cref="Wmi.TruncatedError"/>),
    /// the headline says "1000+" — never an exact count implying
    /// completeness — and a spec row states the list was truncated at
    /// 1000 rows and the counts cover the first 1000 drivers only. The
    /// "Installed drivers" value itself stays the number of rows actually
    /// read (1000), qualified by that row.
    /// </summary>
    internal static ComponentSnapshot BuildSnapshot(IReadOnlyList<WmiRow> drivers, string? queryError)
    {
        const string id = "drivers";
        const string name = "Drivers";
        const string category = "Security";

        var truncated = string.Equals(queryError, Wmi.TruncatedError, StringComparison.Ordinal);
        var now = DateTimeOffset.UtcNow;
        var unsigned = drivers.Count(r => r.Bool("IsSigned") is false);
        var unknown = drivers.Count(r => r.Bool("IsSigned") is null);
        var dated = drivers
            .Select(r => new { Row = r, Date = r.Date("DriverDate") })
            .Where(x => x.Date is not null)
            .ToList();
        var old = dated.Count(x => CollectorSupport.DriverAgeYears(x.Date!.Value, now) > CollectorSupport.DriverStaleAgeYears);
        var oldest = dated.OrderBy(x => x.Date!.Value).FirstOrDefault();

        // "All signed" is only true when every signature is known and
        // signed: drivers with an unknown signature state must not be
        // counted as signed. When truncated, the count is "1000+": the
        // list held more drivers than the row cap returned, so an exact
        // number would imply a completeness the data does not have.
        var countText = truncated ? "1000+" : drivers.Count.ToString(CultureInfo.InvariantCulture);
        var headline = string.Create(
            CultureInfo.InvariantCulture,
            $"{countText} drivers - {(unsigned == 0 && unknown == 0 ? "all signed" : unsigned > 0 ? $"{unsigned} unsigned" : $"{unknown} with unknown signature")}");

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(SpecLabels.InstalledDrivers, drivers.Count.ToString(CultureInfo.InvariantCulture), "CIM Win32_PnPSignedDriver", numericValue: drivers.Count, numericUnit: "drivers"),
            CollectorSupport.Spec(SpecLabels.UnsignedDrivers, unsigned.ToString(CultureInfo.InvariantCulture), "CIM Win32_PnPSignedDriver.IsSigned", numericValue: unsigned, numericUnit: "drivers"),
            CollectorSupport.Spec(SpecLabels.DriversOlderThan5Years, old.ToString(CultureInfo.InvariantCulture), "CIM Win32_PnPSignedDriver.DriverDate", numericValue: old, numericUnit: "drivers"),
            CollectorSupport.Spec(SpecLabels.OldestDriverDate, oldest?.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "CIM Win32_PnPSignedDriver.DriverDate"),
            CollectorSupport.Spec(SpecLabels.OldestDriverDevice, oldest?.Row.String("DeviceName"), "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec(SpecLabels.VeryOldDriverFlag, old switch
            {
                0 => "No",
                > 0 => "Yes",
                _ => SpecLabels.NotReportedByHardware,
            }, "CIM Win32_PnPSignedDriver.DriverDate older than 5 years"),
        };

        // A plain literal label: no new SpecLabels constant (the
        // hardening batch owns SpecLabels). The row sits right after the
        // installed-drivers count so the qualification is next to the
        // number it qualifies.
        if (truncated)
        {
            specs.Insert(1, CollectorSupport.Spec(
                "Driver list truncated",
                "Truncated at 1000 rows; counts cover the first 1000 drivers only.",
                "CIM Win32_PnPSignedDriver"));
        }

        // Two drivers can share a DeviceName; duplicate spec labels
        // collapse in the comparison view, so number repeats.
        var usedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in dated
                     .Where(x => CollectorSupport.DriverAgeYears(x.Date!.Value, now) > CollectorSupport.DriverStaleAgeYears)
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

        return ComponentSnapshot.NotMeasurable(
            id,
            name,
            category,
            id,
            headline,
            specs,
            "Drivers are the small programs that let Windows talk to hardware. All signed drivers is a good sign. A few very old drivers are normal, but many old drivers can cause instability.");
    }
}
