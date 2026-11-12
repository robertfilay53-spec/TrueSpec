/// <summary>
/// The burn tests saturate every logical core, so they must never run in
/// parallel with each other: tests in one collection run sequentially.
/// </summary>
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TrueSpec.Tests;

/// <summary>
/// The burn tests saturate every logical core, so they must never run in
/// parallel with each other: tests in one collection run sequentially.
/// (xUnit v2 has no per-collection parallelization switch. The escalation
/// to assembly-wide CollectionBehavior was applied Nov 13, 2026 after the
/// timing-sensitive ScanThreadPool and SlowCollectors tests failed on three
/// consecutive CI runs of the merged v1.5 branch, while the same code passed
/// fully green on the PR branch. Disabling test parallelization removes the
/// cross-test timing interference that starved those tests.)
/// </summary>
[CollectionDefinition("LoadTestBurn")]
public class LoadTestBurnCollection
{
}
