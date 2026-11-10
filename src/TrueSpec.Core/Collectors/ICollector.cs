namespace TrueSpec.Core.Collectors;

using TrueSpec.Core.Models;

/// <summary>
/// The one contract every data source implements. New hardware support is a new
/// collector, never a change to the core. Windows builds use the CIM/WMI
/// collectors; other platforms use the honest unavailable catalogue.
/// </summary>
public interface ICollector
{
    /// <summary>Stable machine id, for example "storage.nvme".</summary>
    string Id { get; }

    /// <summary>Human readable name shown in the UI.</summary>
    string Name { get; }

    /// <summary>Component category used by the scoring engine and card grid.</summary>
    string Category { get; }

    /// <summary>How often a live collector should refresh.</summary>
    TimeSpan RefreshInterval { get; }

    /// <summary>True when the collector needs an elevated process.</summary>
    bool RequiresAdministrator { get; }

    /// <summary>Collects one snapshot of this component.</summary>
    Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default);
}
