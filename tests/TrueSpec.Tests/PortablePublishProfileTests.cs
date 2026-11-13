namespace TrueSpec.Tests;

/// <summary>
/// Portable buyer-kit profile tests (v2.0 U7): the win-x64-portable
/// publish profile must exist and carry the flags that make the build
/// runnable from a USB stick with no .NET installed, and the README must
/// document how to publish it. The tests read the repo tree, not the App
/// assembly, so they run on both CI legs, like LogoAssetTests.
/// </summary>
public class PortablePublishProfileTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TrueSpec.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not find TrueSpec.sln by walking up from " + AppContext.BaseDirectory);
    }

    private static string RepoFile(params string[] relativePath)
    {
        var root = FindRepoRoot();
        var candidate = relativePath.Aggregate(root, Path.Combine);
        Assert.True(File.Exists(candidate), $"Expected repo file missing: {candidate}");
        return candidate;
    }

    [Fact]
    public void PortableProfile_ExistsAndIsSelfContainedSingleFile()
    {
        var profile = File.ReadAllText(RepoFile(
            "src", "TrueSpec.App", "Properties", "PublishProfiles", "win-x64-portable.pubxml"));

        Assert.Contains("<RuntimeIdentifier>win-x64</RuntimeIdentifier>", profile, StringComparison.Ordinal);
        Assert.Contains("<SelfContained>true</SelfContained>", profile, StringComparison.Ordinal);
        Assert.Contains("<PublishSingleFile>true</PublishSingleFile>", profile, StringComparison.Ordinal);
        Assert.Contains("<PublishTrimmed>false</PublishTrimmed>", profile, StringComparison.Ordinal);
        Assert.Contains("win-x64-portable", profile, StringComparison.Ordinal);
    }

    [Fact]
    public void PortableProfile_DoesNotBundleDebugSymbols()
    {
        var profile = File.ReadAllText(RepoFile(
            "src", "TrueSpec.App", "Properties", "PublishProfiles", "win-x64-portable.pubxml"));

        Assert.Contains("<DebugType>none</DebugType>", profile, StringComparison.Ordinal);
        Assert.Contains("<DebugSymbols>false</DebugSymbols>", profile, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_DocumentsThePortableBuyerKit()
    {
        var readme = File.ReadAllText(RepoFile("README.md"));

        Assert.Contains("win-x64-portable", readme, StringComparison.Ordinal);
        Assert.Contains("USB", readme, StringComparison.Ordinal);
        Assert.Contains("no .NET installed", readme, StringComparison.Ordinal);
        Assert.Contains("mixes machines", readme, StringComparison.Ordinal);
        Assert.Contains("TrueSpec.portable", readme, StringComparison.Ordinal);
        Assert.Contains("TRUESPEC_PORTABLE", readme, StringComparison.Ordinal);
    }
}
