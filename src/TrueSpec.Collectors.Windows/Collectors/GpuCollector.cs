namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using System.Numerics;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;

/// <summary>
/// Graphics adapter identity and driver from CIM, with real temperature, load,
/// fan, power and clock sensors from LibreHardwareMonitor.
/// </summary>
public sealed class GpuCollector : ICollector
{
    private readonly ISensorMonitor _sensors;

    public GpuCollector(ISensorMonitor sensors)
    {
        _sensors = sensors;
    }

    public string Id => "gpu";

    public string Name => "GPU";

    public string Category => "GPU";

    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(2);

    public bool RequiresAdministrator => true;

    public async Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        var adapters = Wmi.Query("root\\cimv2", "SELECT Name, AdapterCompatibility, AdapterRAM, DriverVersion, DriverDate, VideoProcessor, VideoModeDescription, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentBitsPerPixel FROM Win32_VideoController");
        if (adapters.Count == 0)
        {
            return ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "CIM did not return Win32_VideoController.");
        }

        var adapter = adapters[0];
        var sensors = await _sensors.ReadAsync(cancellationToken);
        var temperature = sensors.GpuTemperature();
        var load = sensors.Find(SensorKind.Load, "GPU", "GPU Core", "Core");
        var power = sensors.Find(SensorKind.Power, "GPU", "GPU Package", "GPU Power");
        var fan = sensors.Find(SensorKind.Fan, "GPU", "GPU Fan", "Fan");
        var voltage = sensors.Find(SensorKind.Voltage, "GPU", "GPU Core", "Core");
        var clock = sensors.Find(SensorKind.Clock, "GPU", "GPU Core", "Core");

        var temperatureValue = temperature?.Value;
        var throttle = temperatureValue switch
        {
            >= 90d => "Yes",
            not null => "No",
            _ => CollectorSupport.NotReported,
        };

        // AdapterRAM is a uint32: cards with more than 4 GB of VRAM report
        // 0xFFFFFFFF (4294967295), which would display as a wrong "4.3 GB".
        // 0 and UInt64.MaxValue are the same "no reading" sentinel. All are
        // unknown, never 0.
        var adapterRam = adapter.UInt64("AdapterRAM");
        var dedicatedMemory = NormalizeDedicatedMemory(adapterRam);

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(SpecLabels.Model, adapter.String("Name"), "CIM Win32_VideoController"),
            CollectorSupport.Spec(SpecLabels.Vendor, adapter.String("AdapterCompatibility"), "CIM Win32_VideoController"),
            CollectorSupport.Spec(SpecLabels.DedicatedMemory, CollectorSupport.FormatBytesBinary(dedicatedMemory), "CIM Win32_VideoController.AdapterRAM"),
            CollectorSupport.Spec(SpecLabels.DriverVersion, adapter.String("DriverVersion"), "CIM Win32_VideoController"),
            CollectorSupport.Spec(SpecLabels.DriverDate, adapter.DateText("DriverDate"), "CIM Win32_VideoController.DriverDate"),
            CollectorSupport.Spec(SpecLabels.GPUDriverAge, CollectorSupport.FormatDriverAge(adapter.DateText("DriverDate"), DateTimeOffset.UtcNow), "CIM Win32_VideoController.DriverDate"),
            CollectorSupport.Spec(SpecLabels.VideoProcessor, adapter.String("VideoProcessor"), "CIM Win32_VideoController"),
            CollectorSupport.Spec(SpecLabels.CurrentMode, FormatCurrentMode(adapter.UInt32("CurrentHorizontalResolution"), adapter.UInt32("CurrentVerticalResolution"), adapter.UInt32("CurrentBitsPerPixel"), adapter.String("VideoModeDescription")), "CIM Win32_VideoController"),
            CollectorSupport.Spec(SpecLabels.DirectXSupport, "Not reported by hardware", "WMI does not expose the DirectX feature level"),
            CollectorSupport.Spec(SpecLabels.GPUTemperature, FormatSensor(temperature, "C", 1), "LibreHardwareMonitor"),
            CollectorSupport.Spec(SpecLabels.ThermalThrottleFlag, throttle, "LibreHardwareMonitor temperature threshold (90 C)"),
            CollectorSupport.Spec(SpecLabels.GPULoad, FormatSensor(load, "%", 1), "LibreHardwareMonitor"),
            CollectorSupport.Spec(SpecLabels.GPUPower, FormatSensor(power, "W", 1), "LibreHardwareMonitor"),
            CollectorSupport.Spec(SpecLabels.GPUFan, FormatSensor(fan, "RPM", 0), "LibreHardwareMonitor"),
            CollectorSupport.Spec(SpecLabels.GPUVoltage, FormatSensor(voltage, "V", 3), "LibreHardwareMonitor"),
            CollectorSupport.Spec(SpecLabels.GPUClock, FormatSensor(clock, "MHz", 0), "LibreHardwareMonitor"),
        };

        for (var i = 1; i < adapters.Count; i++)
        {
            var extra = adapters[i];
            var prefix = string.Create(CultureInfo.InvariantCulture, $"Adapter {i + 1}");
            specs.Add(CollectorSupport.Spec($"{prefix} - {SpecLabels.Model}", extra.String("Name"), "CIM Win32_VideoController"));
            specs.Add(CollectorSupport.Spec($"{prefix} - {SpecLabels.DriverVersion}", extra.String("DriverVersion"), "CIM Win32_VideoController"));
        }

        return ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            adapter.String("Name") ?? "GPU",
            specs,
            "GPU health is judged from measured temperature and throttling only. A graphics card does not expose a health percentage. Hot spots, fan noise under light work or a very old driver are the warning signs worth checking.");
    }

    /// <summary>
    /// Normalises AdapterRAM: 0, 0xFFFFFFFF (the &gt;4 GB overflow value)
    /// and UInt64.MaxValue all mean "no reading", never a real size.
    /// </summary>
    internal static ulong? NormalizeDedicatedMemory(ulong? adapterRam) =>
        adapterRam is null or 0UL or 0xFFFFFFFFUL or ulong.MaxValue ? null : adapterRam;

    /// <summary>
    /// Current display mode from the structured resolution and bit-depth
    /// fields, for example "1920 x 1080, 32-bit color". The raw
    /// VideoModeDescription string reports the colour COUNT (2^bits per
    /// pixel) instead of the bit depth, so a 32-bit mode reads as
    /// "1920 x 1080 x 4294967296 colors" (2^32); it is only a fallback and
    /// is sanitised the same way, so a 2^32 colour count can never reach
    /// the screen. A missing part is left out rather than guessed.
    /// </summary>
    internal static string FormatCurrentMode(uint? width, uint? height, uint? bitsPerPixel, string? videoModeDescription)
    {
        var resolution = width is null or 0 or uint.MaxValue || height is null or 0 or uint.MaxValue
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{width.Value} x {height.Value}");
        var colour = bitsPerPixel is null or 0 or > 64
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{bitsPerPixel.Value}-bit color");

        if (resolution is not null || colour is not null)
        {
            return resolution is null ? colour! : colour is null ? resolution : $"{resolution}, {colour}";
        }

        return SanitizeVideoModeDescription(videoModeDescription);
    }

    /// <summary>
    /// Converts a raw VideoModeDescription ("1920 x 1080 x 4294967296
    /// colors") into the same "resolution, bit-depth" form. The trailing
    /// number is the colour COUNT (2^bits per pixel), so a power of two is
    /// converted back to its bit depth; anything else keeps the resolution
    /// only. The raw count is never returned.
    /// </summary>
    internal static string SanitizeVideoModeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return CollectorSupport.NotReported;
        }

        var parts = description.Trim().Split(new[] { 'x', 'X' }, StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 3 ||
            !uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var width) ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var height) ||
            width is 0 or uint.MaxValue || height is 0 or uint.MaxValue)
        {
            return CollectorSupport.NotReported;
        }

        var resolution = string.Create(CultureInfo.InvariantCulture, $"{width} x {height}");
        if (parts.Length == 2)
        {
            return resolution;
        }

        var coloursText = parts[2];
        foreach (var suffix in new[] { "colors", "color" })
        {
            if (coloursText.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                coloursText = coloursText[..^suffix.Length].Trim();
                break;
            }
        }

        if (!ulong.TryParse(coloursText, NumberStyles.None, CultureInfo.InvariantCulture, out var colours) ||
            !IsPowerOfTwo(colours))
        {
            return resolution;
        }

        var bits = BitOperations.Log2(colours);
        return bits is >= 1 and <= 64
            ? string.Create(CultureInfo.InvariantCulture, $"{resolution}, {bits}-bit color")
            : resolution;
    }

    private static bool IsPowerOfTwo(ulong value) => value != 0 && (value & (value - 1)) == 0;

    private static string FormatSensor(SensorReading? reading, string unit, int decimals) =>
        reading is { HasValue: true }
            ? CollectorSupport.FormatNumber(reading.Value, unit, decimals)
            : CollectorSupport.NotReported;
}
