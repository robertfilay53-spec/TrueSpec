namespace TrueSpec.Core.Scoring;

using System.Globalization;
using TrueSpec.Core.Health;
using TrueSpec.Core.Models;

public interface IBuyerCheckEvaluator
{
    BuyerCheckResult Evaluate(IEnumerable<ComponentSnapshot> components, BuyerCheckContext? context = null);
}

/// <summary>
/// Buyer-mode rules. Each rule reads a measured spec value and returns pass,
/// warning or fail with a plain English explanation. Missing readings become
/// "Not applicable" so the report never claims a fake pass or fail.
/// </summary>
public sealed class BuyerCheckEvaluator : IBuyerCheckEvaluator
{
    public BuyerCheckResult Evaluate(IEnumerable<ComponentSnapshot> components, BuyerCheckContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(components);

        var byId = components.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        var checks = new List<BuyerCheck>
        {
            CheckSsdWear(byId),
            CheckBatteryCycles(byId),
            CheckPowerOnHours(byId),
            CheckAdvertisedRam(byId, context),
            CheckBiosDate(byId),
            CheckWindowsInstallDate(byId),
            CheckCriticalErrors(byId),
            CheckSignedDrivers(byId),
        };

        return new BuyerCheckResult(checks);
    }

    private static BuyerCheck CheckSsdWear(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "ssd-wear";
        const string title = "SSD wear is 0%";

        if (!TryGetSpec(byId, "storage", out var component, "Wear level") ||
            !SpecText.TryParseLeadingNumber(component, out var wear))
        {
            return new BuyerCheck(id, title,
                "No SSD wear reading was available.",
                BuyerCheckState.NotApplicable,
                "Ask the seller for a screenshot of the drive SMART data.");
        }

        var smartWarning = SpecValue(byId, "storage", "SMART warnings");
        var extra = string.Equals(smartWarning, "Yes", StringComparison.OrdinalIgnoreCase)
            ? " The drive also reports SMART warnings."
            : string.Empty;

        return wear <= 0d && extra.Length == 0
            ? new BuyerCheck(id, title,
                "The drive reports 0% wear and no SMART warnings.",
                BuyerCheckState.Pass,
                "Nothing to ask.")
            : new BuyerCheck(id, title,
                FormattableString.Invariant($"The drive reports {wear:0.#}% wear.{extra}"),
                BuyerCheckState.Fail,
                "Ask why the drive has wear if the laptop is being sold as new.");
    }

    private static BuyerCheck CheckBatteryCycles(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "battery-cycles";
        const string title = "Battery cycles 5 or fewer";

        if (!TryGetSpec(byId, "battery", out var value, "Cycle count") ||
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cycles))
        {
            return new BuyerCheck(id, title,
                "The battery did not report a cycle count.",
                BuyerCheckState.NotApplicable,
                "Ask the seller to run a battery report and show the cycle count.");
        }

        return cycles <= 5
            ? new BuyerCheck(id, title,
                $"The battery reports {cycles} charge cycles.",
                BuyerCheckState.Pass,
                "Nothing to ask.")
            : new BuyerCheck(id, title,
                $"The battery reports {cycles} charge cycles, which means it has been charged many times.",
                BuyerCheckState.Fail,
                "Ask how the laptop was used before it was sold.");
    }

    private static BuyerCheck CheckPowerOnHours(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "power-on-hours";
        const string title = "Drive power-on hours are 0";

        if (!TryGetSpec(byId, "storage", out var value, "Power-on hours") ||
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours))
        {
            return new BuyerCheck(id, title,
                "No drive power-on hour count was available.",
                BuyerCheckState.NotApplicable,
                "Ask the seller for a screenshot of the drive SMART data.");
        }

        return hours == 0
            ? new BuyerCheck(id, title,
                "The drive reports 0 power-on hours.",
                BuyerCheckState.Pass,
                "Nothing to ask.")
            : new BuyerCheck(id, title,
                $"The drive has been powered on for {hours} hours.",
                BuyerCheckState.Fail,
                "Ask how a new laptop already has powered-on hours on its drive.");
    }

    private static BuyerCheck CheckAdvertisedRam(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        BuyerCheckContext? context)
    {
        const string id = "ram-size";
        const string title = "RAM size matches the advertisement";

        if (!TryGetSpec(byId, "ram", out var actual, "Total memory"))
        {
            return new BuyerCheck(id, title,
                "The installed RAM size was not available.",
                BuyerCheckState.NotApplicable,
                "Ask the seller for the exact RAM size.");
        }

        var advertised = context?.AdvertisedRam;
        if (string.IsNullOrWhiteSpace(advertised))
        {
            return new BuyerCheck(id, title,
                $"Installed memory is {actual}. No advertised size was entered.",
                BuyerCheckState.NotApplicable,
                "Type the RAM size from the seller listing to enable this check.");
        }

        return string.Equals(NormalizeSize(actual), NormalizeSize(advertised), StringComparison.OrdinalIgnoreCase)
            ? new BuyerCheck(id, title,
                $"Installed memory is {actual} and the listing says {advertised}.",
                BuyerCheckState.Pass,
                "Nothing to ask.")
            : new BuyerCheck(id, title,
                $"Installed memory is {actual} but the listing says {advertised}.",
                BuyerCheckState.Fail,
                "Ask the seller to explain the difference before you buy.");
    }

    private static BuyerCheck CheckBiosDate(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "bios-date";
        const string title = "BIOS date is recent";

        if (!TryGetSpec(byId, "system", out var value, "BIOS date") ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var biosDate))
        {
            return new BuyerCheck(id, title,
                "The BIOS date was not available.",
                BuyerCheckState.NotApplicable,
                "Ask the seller for the BIOS version and date.");
        }

        var age = DateTimeOffset.UtcNow - biosDate;
        return age.TotalDays switch
        {
            <= 730 => new BuyerCheck(id, title,
                $"BIOS date is {biosDate:yyyy-MM-dd}, less than two years old.",
                BuyerCheckState.Pass,
                "Nothing to ask."),
            <= 1095 => new BuyerCheck(id, title,
                $"BIOS date is {biosDate:yyyy-MM-dd}, between two and three years old.",
                BuyerCheckState.Warning,
                "Ask whether the BIOS has ever been updated."),
            _ => new BuyerCheck(id, title,
                $"BIOS date is {biosDate:yyyy-MM-dd}, over three years old.",
                BuyerCheckState.Fail,
                "Ask why a laptop sold as new has an old BIOS."),
        };
    }

    private static BuyerCheck CheckWindowsInstallDate(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "windows-install";
        const string title = "Windows install date is recent";

        if (!TryGetSpec(byId, "system", out var value, "Windows install date") ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var installDate))
        {
            return new BuyerCheck(id, title,
                "The Windows install date was not available.",
                BuyerCheckState.NotApplicable,
                "Ask when Windows was last installed.");
        }

        var age = DateTimeOffset.UtcNow - installDate;
        return age.TotalDays switch
        {
            <= 60 => new BuyerCheck(id, title,
                $"Windows was installed on {installDate:yyyy-MM-dd}, within the last two months.",
                BuyerCheckState.Pass,
                "Nothing to ask."),
            <= 180 => new BuyerCheck(id, title,
                $"Windows was installed on {installDate:yyyy-MM-dd}, within the last six months.",
                BuyerCheckState.Warning,
                "Ask whether the machine was reset before sale."),
            _ => new BuyerCheck(id, title,
                $"Windows was installed on {installDate:yyyy-MM-dd}, over six months ago.",
                BuyerCheckState.Fail,
                "Ask how the machine was used before it was sold."),
        };
    }

    private static BuyerCheck CheckCriticalErrors(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "critical-errors";
        const string title = "No critical errors in the event log";

        if (!TryGetSpec(byId, "system", out var value, "Critical errors (event log)") ||
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var errors))
        {
            return new BuyerCheck(id, title,
                "The event log could not be read.",
                BuyerCheckState.NotApplicable,
                "Ask the seller to show the Windows event log.");
        }

        return errors == 0
            ? new BuyerCheck(id, title,
                "No critical errors were found.",
                BuyerCheckState.Pass,
                "Nothing to ask.")
            : new BuyerCheck(id, title,
                $"{errors} critical error(s) were found in the event log.",
                BuyerCheckState.Warning,
                "Ask what caused the errors and whether the machine has crashed.");
    }

    private static BuyerCheck CheckSignedDrivers(IReadOnlyDictionary<string, ComponentSnapshot> byId)
    {
        const string id = "signed-drivers";
        const string title = "All drivers are signed";

        if (!TryGetSpec(byId, "system", out var value, "Signed drivers only"))
        {
            return new BuyerCheck(id, title,
                "The driver signature state was not available.",
                BuyerCheckState.NotApplicable,
                "Ask the seller whether any unsigned drivers are installed.");
        }

        if (string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase))
        {
            return new BuyerCheck(id, title,
                "Only signed drivers are installed.",
                BuyerCheckState.Pass,
                "Nothing to ask.");
        }

        if (string.Equals(value, "No", StringComparison.OrdinalIgnoreCase))
        {
            return new BuyerCheck(id, title,
                "Unsigned drivers were found.",
                BuyerCheckState.Fail,
                "Ask why unsigned drivers were installed on the machine.");
        }

        return new BuyerCheck(id, title,
            "Windows did not report a complete driver signature state.",
            BuyerCheckState.NotApplicable,
            "Ask the seller whether any unsigned drivers are installed.");
    }

    private static bool TryGetSpec(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        string componentId,
        out string value,
        string label)
    {
        value = string.Empty;
        if (!byId.TryGetValue(componentId, out var component))
        {
            return false;
        }

        var spec = component.Specs.FirstOrDefault(s =>
            string.Equals(s.Label, label, StringComparison.OrdinalIgnoreCase));
        if (spec is null || string.IsNullOrWhiteSpace(spec.Value))
        {
            return false;
        }

        value = spec.Value.Trim();
        return true;
    }

    private static string? SpecValue(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        string componentId,
        string label) =>
        byId.TryGetValue(componentId, out var component)
            ? component.Specs.FirstOrDefault(s =>
                string.Equals(s.Label, label, StringComparison.OrdinalIgnoreCase))?.Value
            : null;

    private static string NormalizeSize(string text) =>
        text.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("GB", "GB", StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();
}
