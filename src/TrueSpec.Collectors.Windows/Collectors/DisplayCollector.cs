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
        var monitors = Wmi.Query("root\\wmi", "SELECT * FROM WmiMonitorID");
        var video = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_VideoController").FirstOrDefault();

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
        var basic = Wmi.Query("root\\wmi", "SELECT * FROM WmiMonitorBasicDisplayParams").FirstOrDefault();

        var manufacturer = Decode(monitor?.UInt16Array("ManufacturerName"));
        var model = Decode(monitor?.UInt16Array("ProductCodeID"));
        var serial = Decode(monitor?.UInt16Array("SerialNumberID"));
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
        var refresh = video?.UInt32("CurrentRefreshRate") is { } hz && hz > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hz} Hz")
            : CollectorSupport.NotReported;

        var headline = string.Join(" ", new[]
        {
            friendly ?? model,
            resolution == CollectorSupport.NotReported ? null : resolution,
            refresh == CollectorSupport.NotReported ? null : refresh,
        }.Where(v => !string.IsNullOrWhiteSpace(v)));

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec("Panel manufacturer", manufacturer, "EDID WmiMonitorID.ManufacturerName"),
            CollectorSupport.Spec("Panel model", model, "EDID WmiMonitorID.ProductCodeID"),
            CollectorSupport.Spec("Panel serial", serial, "EDID WmiMonitorID.SerialNumberID"),
            CollectorSupport.Spec("Panel name", friendly, "EDID WmiMonitorID.UserFriendlyName"),
            CollectorSupport.Spec("Manufacture date", manufactureDate, "EDID WmiMonitorID manufacture week/year"),
            CollectorSupport.Spec("Size", size, "EDID WmiMonitorBasicDisplayParams"),
            CollectorSupport.Spec("Resolution", resolution, "CIM Win32_VideoController"),
            CollectorSupport.Spec("Refresh rate", refresh, "CIM Win32_VideoController"),
            CollectorSupport.Spec("Monitors detected", monitors.Count.ToString(CultureInfo.InvariantCulture), "CIM WmiMonitorID"),
            CollectorSupport.Spec("HDR", "Not reported by hardware", "EDID extended block not exposed by WmiMonitorID"),
            CollectorSupport.Spec("Panel type", "Not reported by hardware", "EDID panel type is not exposed by WmiMonitorID"),
        };

        for (var i = 1; i < monitors.Count; i++)
        {
            var extra = monitors[i];
            var prefix = string.Create(CultureInfo.InvariantCulture, $"Monitor {i + 1}");
            specs.Add(CollectorSupport.Spec($"{prefix} - Panel model", Decode(extra.UInt16Array("ProductCodeID")), "EDID WmiMonitorID"));
            specs.Add(CollectorSupport.Spec($"{prefix} - Panel serial", Decode(extra.UInt16Array("SerialNumberID")), "EDID WmiMonitorID"));
            specs.Add(CollectorSupport.Spec($"{prefix} - Manufacture date",
                extra.UInt16("YearOfManufacture") is { } extraYear && extraYear > 0
                    ? string.Create(CultureInfo.InvariantCulture, $"{extraYear} week {extra.UInt16("WeekOfManufacture") ?? 0}")
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
            "The panel block shows what screen is fitted. The manufacture date comes from the panel EDID itself. A panel made long before the laptop was sold can be a sign of a replaced screen. HDR and panel technology are only shown when the EDID exposes them."));
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

    private static string FormatResolution(WmiRow? video)
    {
        var width = video?.UInt32("CurrentHorizontalResolution");
        var height = video?.UInt32("CurrentVerticalResolution");
        return width is null or 0 || height is null or 0
            ? CollectorSupport.NotReported
            : string.Create(CultureInfo.InvariantCulture, $"{width.Value} x {height.Value}");
    }

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
