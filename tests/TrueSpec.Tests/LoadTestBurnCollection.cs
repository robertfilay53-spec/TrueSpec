namespace TrueSpec.Tests;

/// <summary>
/// The burn tests saturate every logical core, so they must never run in
/// parallel with each other: tests in one collection run sequentially.
/// (xUnit v2 has no per-collection parallelization switch; if a burn ever
/// starves a timing-sensitive test in another collection again, the
/// escalation is assembly-wide CollectionBehavior.)
/// </summary>
[CollectionDefinition("LoadTestBurn")]
public class LoadTestBurnCollection
{
}
