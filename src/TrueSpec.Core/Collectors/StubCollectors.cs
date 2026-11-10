namespace TrueSpec.Core.Collectors;

using TrueSpec.Core.Models;

/// <summary>
/// P0 collector that returns realistic sample data. It exists so the UI can be
/// built and reviewed before any real hardware access is written. Replace with
/// a real CIM/WMI collector in P1 without touching the app.
/// </summary>
public sealed class StubCollector : ICollector
{
    private readonly Func<ComponentSnapshot> _factory;

    public StubCollector(
        string id,
        string name,
        string category,
        Func<ComponentSnapshot> factory,
        bool requiresAdministrator = false,
        TimeSpan? refreshInterval = null)
    {
        Id = id;
        Name = name;
        Category = category;
        _factory = factory;
        RequiresAdministrator = requiresAdministrator;
        RefreshInterval = refreshInterval ?? TimeSpan.FromSeconds(2);
    }

    public string Id { get; }

    public string Name { get; }

    public string Category { get; }

    public TimeSpan RefreshInterval { get; }

    public bool RequiresAdministrator { get; }

    public bool IsStub => true;

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_factory());
    }
}

/// <summary>The P0 set of stub collectors, one per dashboard component.</summary>
public static class StubCollectorCatalog
{
    public static IReadOnlyList<ICollector> CreateAll() => new ICollector[]
    {
        new StubCollector("system", "System", "System", SampleData.System),
        new StubCollector("cpu", "CPU", "CPU", SampleData.Cpu),
        new StubCollector("ram", "RAM", "RAM", SampleData.Ram),
        new StubCollector("storage", "Storage", "Storage", SampleData.Storage, requiresAdministrator: true),
        new StubCollector("battery", "Battery", "Battery", SampleData.Battery),
        new StubCollector("gpu", "GPU", "GPU", SampleData.Gpu),
        new StubCollector("display", "Display", "Display", SampleData.Display),
        new StubCollector("network", "Network", "Network", SampleData.Network),
        new StubCollector("motherboard", "Motherboard", "Motherboard", SampleData.Motherboard),
        new StubCollector("security", "Security", "Security", SampleData.Security),
    };

    public static StubCollector Drivers() =>
        new("drivers", "Drivers", "Security", SampleData.Drivers);
}
