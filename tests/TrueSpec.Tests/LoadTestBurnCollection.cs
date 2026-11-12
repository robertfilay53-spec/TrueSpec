namespace TrueSpec.Tests;

/// <summary>
/// The burn tests saturate every logical core, so they must never run in
/// parallel with each other or with timing-sensitive tests (a scan-timeout
/// test once took 3.27 s instead of under 2 s while burns ran beside it).
/// Tests in this collection run serially against the whole suite.
/// </summary>
[CollectionDefinition("LoadTestBurn", DisableTestParallelization = true)]
public class LoadTestBurnCollection
{
}
