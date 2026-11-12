namespace TrueSpec.Tests;

/// <summary>
/// F12 logo asset test (hardening item 7): the logo files the app and the
/// installer reference must exist and be non-empty. Before this test there
/// was zero coverage: a renamed or deleted logo file failed only at
/// runtime (missing window icon) or at installer build time.
/// The test reads the repo tree, not the App assembly, so it runs on both
/// CI legs.
/// </summary>
public class LogoAssetTests
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

    private static void AssertNonEmptyFile(string path)
    {
        var info = new FileInfo(path);
        Assert.True(info.Length > 0, $"Logo asset is empty: {path}");
    }

    [Fact]
    public void ReferencedLogoAssets_ExistAndAreNonEmpty()
    {
        var ico = RepoFile("src", "TrueSpec.App", "Assets", "truespec-logo.ico");
        var png = RepoFile("src", "TrueSpec.App", "Assets", "truespec-logo.png");
        var lockup = RepoFile("src", "TrueSpec.App", "Assets", "truespec-logo-lockup.png");

        AssertNonEmptyFile(ico);
        AssertNonEmptyFile(png);
        AssertNonEmptyFile(lockup);
    }

    [Fact]
    public void AppProjectAndWindow_ReferenceTheLogoIcon()
    {
        var csproj = File.ReadAllText(RepoFile("src", "TrueSpec.App", "TrueSpec.App.csproj"));
        Assert.Contains("truespec-logo.ico", csproj, StringComparison.Ordinal);

        var window = File.ReadAllText(RepoFile("src", "TrueSpec.App", "Views", "MainWindow.axaml"));
        Assert.Contains("truespec-logo.ico", window, StringComparison.Ordinal);
        Assert.Contains("truespec-logo.png", window, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ReferencesTheLogoIcon()
    {
        var iss = File.ReadAllText(RepoFile("installer", "TrueSpec.iss"));
        Assert.Contains("truespec-logo.ico", iss, StringComparison.Ordinal);
    }
}
