namespace TrueSpec.Core.Collectors;

using TrueSpec.Core.Models;

/// <summary>
/// Fallback collectors used on platforms where Windows CIM/WMI and hardware
/// sensors are not available. They never invent a reading: every component is
/// returned as "Not available" with the reason named.
/// </summary>
public sealed class UnavailableCollector : ICollector
{
    private readonly string _reason;

    public UnavailableCollector(string id, string name, string category, string iconKey, string reason)
    {
        Id = id;
        Name = name;
        Category = category;
        IconKey = iconKey;
        _reason = reason;
    }

    public string Id { get; }

    public string Name { get; }

    public string Category { get; }

    public string IconKey { get; }

    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(2);

    public bool RequiresAdministrator => false;

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ComponentSnapshot.NotAvailable(Id, Name, Category, IconKey, _reason));
    }
}

/// <summary>
/// The honest fallback catalogue. The desktop app uses it on non-Windows
/// machines so the UI still opens without showing a single fake value.
/// </summary>
public static class UnavailableCollectorCatalog
{
    public const string DefaultReason =
        "Not available on this platform. Windows CIM/WMI and hardware sensor access are required.";

    public static IReadOnlyList<ICollector> CreateAll(string? reason = null) => new ICollector[]
    {
        Create("system", "System", "System", "system", reason),
        Create("cpu", "CPU", "CPU", "cpu", reason),
        Create("ram", "RAM", "RAM", "ram", reason),
        Create("storage", "Storage", "Storage", "storage", reason),
        Create("battery", "Battery", "Battery", "battery", reason),
        Create("gpu", "GPU", "GPU", "gpu", reason),
        Create("display", "Display", "Display", "display", reason),
        Create("network", "Network", "Network", "network", reason),
        Create("motherboard", "Motherboard", "Motherboard", "motherboard", reason),
        Create("security", "Security", "Security", "security", reason),
        Create("drivers", "Drivers", "Security", "drivers", reason),
    };

    private static UnavailableCollector Create(
        string id,
        string name,
        string category,
        string iconKey,
        string? reason) =>
        new(id, name, category, iconKey, reason ?? DefaultReason);
}
