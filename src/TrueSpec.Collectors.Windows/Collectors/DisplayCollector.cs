namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// Panel identity and manufacture week/year from the monitor EDID through
/// WmiMonitorID. Refresh rate and resolution come from the graphics adapter.
/// </summary>
public sealed class DisplayCollector : ICollector
{
    public string Id => "display";

    public string Name => "Display";

    public string Category => "Display";

    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(10);

    public bool RequiresAdministrator => false;

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        var monitors = Wmi.Query("root\\wmi", "SELECT ManufacturerName, ProductCodeID, SerialNumberID, UserFriendlyName, YearOfManufacture, WeekOfManufacture FROM WmiMonitorID");
        var video = Wmi.Query("root\\cimv2", "SELECT CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate, CurrentBitsPerPixel FROM Win32_VideoController").FirstOrDefault();

        if (monitors.Count == 0 && video is null)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "CIM did not return WmiMonitorID or Win32_VideoController."));
        }

        var monitor = monitors.FirstOrDefault();
        var basic = Wmi.Query("root\\wmi", "SELECT MaxHorizontalImageSize, MaxVerticalImageSize FROM WmiMonitorBasicDisplayParams").FirstOrDefault();

        var manufacturer = Decode(monitor?.UInt16Array("ManufacturerName"));
        var model = Decode(monitor?.UInt16Array("ProductCodeID"));
        var serial = FormatSerial(Decode(monitor?.UInt16Array("SerialNumberID")));
        var friendly = Decode(monitor?.UInt16Array("UserFriendlyName"));
        var year = monitor?.UInt16("YearOfManufacture");
        var week = monitor?.UInt16("WeekOfManufacture");

        var manufactureDate = year is null or 0
            ? CollectorSupport.NotReported
            : week is null or 0
                ? string.Create(CultureInfo.InvariantCulture, $"{year.Value}")
                : string.Create(CultureInfo.InvariantCulture, $"{year.Value} week {week.Value}");

        var size = FormatPanelSize(basic);
        var resolution = FormatResolution(video);
        var colourDepth = FormatColourDepth(video?.UInt32("CurrentBitsPerPixel"));
        var refresh = FormatRefreshRate(video?.UInt32("CurrentRefreshRate"));

        var headline = BuildHeadline(friendly, model, resolution, refresh);

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(SpecLabels.PanelManufacturer, manufacturer, "EDID WmiMonitorID.ManufacturerName"),
            CollectorSupport.Spec(SpecLabels.PanelModel, model, "EDID WmiMonitorID.ProductCodeID"),
            CollectorSupport.Spec(SpecLabels.PanelSerial, serial, "EDID WmiMonitorID.SerialNumberID"),
            CollectorSupport.Spec(SpecLabels.PanelName, friendly, "EDID WmiMonitorID.UserFriendlyName"),
            CollectorSupport.Spec(SpecLabels.ManufactureDate, manufactureDate, "EDID WmiMonitorID manufacture week/year"),
            CollectorSupport.Spec(SpecLabels.Size, size, "EDID WmiMonitorBasicDisplayParams"),
            // U11: the panel block (EDID) sits next to the active Windows
            // resolution so a buyer can compare them. The panel's own native
            // resolution is NOT shown as a number: EDID detailed timings are
            // not exposed by WmiMonitorID, so it is reported as not available
            // rather than guessed from the active resolution.
            CollectorSupport.Spec(SpecLabels.ActiveResolution, resolution, "CIM Win32_VideoController (active display settings)"),
            CollectorSupport.Spec(SpecLabels.NativeResolution, null, "EDID detailed timings are not exposed by WmiMonitorID"),
            CollectorSupport.Spec(SpecLabels.ColourDepth, colourDepth, "CIM Win32_VideoController.CurrentBitsPerPixel"),
            CollectorSupport.Spec(SpecLabels.RefreshRate, refresh, "CIM Win32_VideoController"),
            CollectorSupport.Spec(SpecLabels.MonitorsDetected, monitors.Count.ToString(CultureInfo.InvariantCulture), "CIM WmiMonitorID"),
            CollectorSupport.Spec(SpecLabels.HDR, "Not reported by hardware", "EDID extended block not exposed by WmiMonitorID"),
            CollectorSupport.Spec(SpecLabels.PanelType, "Not reported by hardware", "EDID panel type is not exposed by WmiMonitorID"),
        };

        for (var i = 1; i < monitors.Count; i++)
        {
            var extra = monitors[i];
            var prefix = string.Create(CultureInfo.InvariantCulture, $"Monitor {i + 1}");
            specs.Add(CollectorSupport.Spec($"{prefix} - {SpecLabels.PanelModel}", Decode(extra.UInt16Array("ProductCodeID")), "EDID WmiMonitorID"));
            specs.Add(CollectorSupport.Spec($"{prefix} - {SpecLabels.PanelSerial}", FormatSerial(Decode(extra.UInt16Array("SerialNumberID"))), "EDID WmiMonitorID"));
            // Same rule as the first monitor: a missing/zero week means
            // "year only", never "week 0", which is not a real date.
            var extraWeek = extra.UInt16("WeekOfManufacture");
            specs.Add(CollectorSupport.Spec($"{prefix} - {SpecLabels.ManufactureDate}",
                extra.UInt16("YearOfManufacture") is { } extraYear && extraYear > 0
                    ? extraWeek is null or 0
                        ? string.Create(CultureInfo.InvariantCulture, $"{extraYear}")
                        : string.Create(CultureInfo.InvariantCulture, $"{extraYear} week {extraWeek.Value}")
                    : null,
                "EDID WmiMonitorID manufacture week/year"));
        }

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            string.IsNullOrWhiteSpace(headline) ? "Display" : headline,
            specs,
            "The panel block shows what screen is fitted. The manufacture date comes from the panel EDID itself. A panel made long before the laptop was sold can be a sign of a replaced screen. The resolution shown is the one Windows is currently using; the panel's own native resolution is not exposed by WmiMonitorID, so it is reported as not available rather than guessed. HDR and panel technology are only shown when the EDID exposes them. " +
            BuildBuyerSummary(manufacturer, model, manufactureDate, colourDepth, resolution)));
    }

    /// <summary>
    /// U11 one-line buyer summary: the panel's real identity (EDID) next to
    /// the resolution Windows is currently using, so a buyer can compare
    /// them at a glance. Any part the hardware did not report says so
    /// instead of being guessed.
    /// </summary>
    internal static string BuildBuyerSummary(
        string manufacturer,
        string model,
        string manufactureDate,
        string colourDepth,
        string resolution)
    {
        var identityParts = new[] { manufacturer, model }
            .Where(v => !string.Equals(v, CollectorSupport.NotReported, StringComparison.Ordinal))
            .ToArray();
        var panelPart = identityParts.Length == 0
            ? "panel manufacturer and model not reported by hardware"
            : string.Create(CultureInfo.InvariantCulture, $"panel {string.Join(" ", identityParts)}");

        var madePart = string.Equals(manufactureDate, CollectorSupport.NotReported, StringComparison.Ordinal)
            ? "manufacture date not reported by hardware"
            : string.Create(CultureInfo.InvariantCulture, $"made {manufactureDate}");

        var colourPart = string.Equals(colourDepth, CollectorSupport.NotReported, StringComparison.Ordinal)
            ? "colour depth not reported by hardware"
            : string.Create(CultureInfo.InvariantCulture, $"colour depth {colourDepth}");

        var activePart = string.Equals(resolution, CollectorSupport.NotReported, StringComparison.Ordinal)
            ? "Windows is not reporting an active resolution"
            : string.Create(CultureInfo.InvariantCulture, $"Windows is using {resolution}");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Buyer check: {panelPart}, {madePart}, {colourPart}; {activePart}.");
    }

    private static string Decode(ushort[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return CollectorSupport.NotReported;
        }

        var chars = values
            .Where(v => v != 0)
            .Select(v => (char)v)
            .ToArray();

        return chars.Length == 0 ? CollectorSupport.NotReported : new string(chars).Trim();
    }

    /// <summary>
    /// Card headline: the panel name when EDID reports one, then the real
    /// active resolution and refresh. A missing panel name must never
    /// print as "Not reported by hardware" in front of real values: only
    /// the missing field is unreported, and the Panel name spec says so.
    /// Returns an empty string when nothing is reported; the caller falls
    /// back to "Display".
    /// </summary>
    internal static string BuildHeadline(string? friendlyName, string? model, string resolution, string refreshRate)
    {
        string? name = null;
        foreach (var candidate in new[] { friendlyName, model })
        {
            if (!string.IsNullOrWhiteSpace(candidate) &&
                !string.Equals(candidate, CollectorSupport.NotReported, StringComparison.Ordinal))
            {
                name = candidate;
                break;
            }
        }

        string? displayPart = resolution == CollectorSupport.NotReported
            ? refreshRate == CollectorSupport.NotReported ? null : refreshRate
            : refreshRate == CollectorSupport.NotReported
                ? resolution
                : string.Create(CultureInfo.InvariantCulture, $"{resolution}, {refreshRate}");

        return name is null
            ? displayPart ?? string.Empty
            : displayPart is null
                ? name
                : string.Create(CultureInfo.InvariantCulture, $"{name} {displayPart}");
    }

    /// <summary>
    /// Panel serial from EDID. A serial of "0" (or all zeroes) means the
    /// panel vendor never programmed one: it is not a serial number, so it
    /// shows as "Not reported by hardware" instead of "0". A serial with
    /// leading zeroes followed by other characters is kept as reported.
    /// </summary>
    internal static string FormatSerial(string decoded)
    {
        if (string.Equals(decoded, CollectorSupport.NotReported, StringComparison.Ordinal))
        {
            return CollectorSupport.NotReported;
        }

        var serial = decoded.Trim();
        return serial.Length == 0 || serial.Trim('0').Length == 0
            ? CollectorSupport.NotReported
            : serial;
    }

    /// <summary>
    /// U11: colour depth from Win32_VideoController.CurrentBitsPerPixel, as
    /// "32-bit". A missing, zero or impossible value (no panel reports
    /// more than 64 bits per pixel; UInt32.MaxValue is the "unknown"
    /// sentinel) is "Not reported by hardware": 0 bits per pixel is not
    /// a real colour depth.
    /// </summary>
    internal static string FormatColourDepth(uint? bitsPerPixel) =>
        bitsPerPixel is null or 0 or > 64
            ? CollectorSupport.NotReported
            : string.Create(CultureInfo.InvariantCulture, $"{bitsPerPixel.Value}-bit");

    internal static string FormatResolution(uint? width, uint? height) =>
        width is null or 0 or uint.MaxValue || height is null or 0 or uint.MaxValue
            ? CollectorSupport.NotReported
            : string.Create(CultureInfo.InvariantCulture, $"{width.Value} x {height.Value}");

    private static string FormatResolution(WmiRow? video) =>
        FormatResolution(video?.UInt32("CurrentHorizontalResolution"), video?.UInt32("CurrentVerticalResolution"));

    /// <summary>
    /// Refresh rate from Win32_VideoController.CurrentRefreshRate. A
    /// missing, zero or UInt32.MaxValue ("unknown" sentinel) value is
    /// "Not reported by hardware", never "0 Hz" or a billions-of-Hz number.
    /// </summary>
    internal static string FormatRefreshRate(uint? hertz) =>
        hertz is null or 0 or uint.MaxValue
            ? CollectorSupport.NotReported
            : string.Create(CultureInfo.InvariantCulture, $"{hertz.Value} Hz");

    private static string FormatPanelSize(WmiRow? basic)
    {
        var widthCm = basic?.UInt16("MaxHorizontalImageSize");
        var heightCm = basic?.UInt16("MaxVerticalImageSize");
        if (widthCm is null or 0 || heightCm is null or 0)
        {
            return CollectorSupport.NotReported;
        }

        var diagonalCm = Math.Sqrt((widthCm.Value * widthCm.Value) + (heightCm.Value * heightCm.Value));
        return string.Create(CultureInfo.InvariantCulture, $"{diagonalCm / 2.54d:0.0} inches");
    }
}
