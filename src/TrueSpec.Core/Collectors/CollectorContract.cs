namespace TrueSpec.Core.Collectors;

/// <summary>
/// The versioned collector contract (plug-ins, plan Beyond v2.0 item 2,
/// phase 1). Version 1 is the <see cref="ICollector"/> surface as shipped
/// in v2.1.4: Id, Name, Category, RefreshInterval, RequiresAdministrator,
/// CollectAsync. A plug-in declares the contract version its collectors
/// were written against, and discovery accepts only the current version.
/// Any change to <see cref="ICollector"/> bumps this version and is a
/// deliberate, reviewed decision, never a silent drift: the shape is
/// pinned by CollectorContractTests.
/// </summary>
public static class CollectorContract
{
    /// <summary>The contract version plug-ins target today.</summary>
    public const int CurrentVersion = 1;
}
