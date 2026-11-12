namespace TrueSpec.Core.Collectors;

using TrueSpec.Core.Models;

/// <summary>
/// The one contract every data source implements. New hardware support is a new
/// collector, never a change to the core. Windows builds use the CIM/WMI
/// collectors; other platforms use the honest unavailable catalogue.
/// </summary>
public interface ICollector
{
    /// <summary>Stable machine id, for example "storage".</summary>
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
    /// <remarks>
    /// Throw-vs-empty contract: a collector never throws for missing or
    /// unreadable hardware. When a component cannot be measured it returns
    /// an honest-empty snapshot instead (<see cref="ComponentSnapshot.NotAvailable"/>
    /// when the component is absent, <see cref="ComponentSnapshot.NotMeasurable"/>
    /// when it is present but a reading could not be taken), with every
    /// unmeasured value reading "Not reported by hardware" and the reason
    /// in the headline. The only exception is
    /// <see cref="OperationCanceledException"/> when the caller's token is
    /// cancelled: cancellation is a control signal, not a missing reading.
    /// A collector that threw on a missing battery or an unreadable sensor
    /// would fail the whole scan for one absent part; honest-empty keeps
    /// the scan, the score and the history intact.
    /// </remarks>
    Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default);
}
