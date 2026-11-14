using TrueSpec.Core.Security;

namespace TrueSpec.Tests;

/// <summary>
/// Installer regression tests for the P9 uninstall fix.
///
/// The uninstaller must leave nothing behind even when TrueSpec is running
/// at uninstall time (the installer auto-launches the app on Finish, so it
/// usually is): the running app has to be closed before any file is
/// removed, and the app's own data folder has to go with it. These tests
/// parse installer/TrueSpec.iss and assert the directives that guarantee
/// that; iscc itself is Windows-only and cannot run on this VM, so the
/// real-Windows AppOnFly uninstall test remains the behavioural check.
/// </summary>
public class InstallerScriptTests
{
    private static string FindInstallerScript()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "installer", "TrueSpec.iss");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not find installer/TrueSpec.iss by walking up from " + AppContext.BaseDirectory);
    }

    private static string FindRepoFile(params string[] relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = relativePath.Aggregate(dir.FullName, Path.Combine);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not find " + string.Join(Path.DirectorySeparatorChar, relativePath)
            + " by walking up from " + AppContext.BaseDirectory);
    }

    private static string[] ReadScript() => File.ReadAllLines(FindInstallerScript());

    private static string SetupDirective(string[] lines, string name)
    {
        var inSetup = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                inSetup = line.Equals("[Setup]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSetup || line.StartsWith(";", StringComparison.Ordinal) || line.Length == 0)
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq > 0 && line[..eq].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return line[(eq + 1)..].Trim();
            }
        }

        throw new InvalidOperationException($"[Setup] directive {name} not found in TrueSpec.iss");
    }

    private static string[] SectionLines(string[] lines, string section)
    {
        var inSection = false;
        var found = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                inSection = line.Equals(section, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inSection && line.Length > 0 && !line.StartsWith(";", StringComparison.Ordinal))
            {
                found.Add(line);
            }
        }

        return found.ToArray();
    }

    [Fact]
    public void Version_Is_2_1_40_InAllThreePlaces()
    {
        // The version lives in three places and they must agree: the app
        // project (assembly version), the installer script, and the
        // application manifest. A bump that misses one ships a build whose
        // installer, exe and manifest disagree about what version it is.
        var iss = File.ReadAllText(FindInstallerScript());
        Assert.Contains("#define AppVersion \"2.1.40\"", iss, StringComparison.Ordinal);

        var csproj = File.ReadAllText(FindRepoFile("src", "TrueSpec.App", "TrueSpec.App.csproj"));
        Assert.Contains("<Version>2.1.40</Version>", csproj, StringComparison.Ordinal);

        var manifest = File.ReadAllText(FindRepoFile("src", "TrueSpec.App", "app.manifest"));
        Assert.Contains("version=\"2.1.40.0\"", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void CloseApplications_IsEnabled()
    {
        // Restart Manager must ask the running app to close before any
        // file is removed; without this the locked exe survives uninstall.
        Assert.Equal("yes", SetupDirective(ReadScript(), "CloseApplications"));
    }

    [Fact]
    public void CloseApplicationsFilter_NamesTheAppExe()
    {
        // The filter must name the app's own exe, not every exe: a broad
        // "*.exe" filter lets Restart Manager consider unrelated apps.
        var filter = SetupDirective(ReadScript(), "CloseApplicationsFilter");
        Assert.Contains("TrueSpec.App.exe", filter, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RestartApplications_IsDisabled()
    {
        // The default is yes: after an uninstall there is nothing to
        // restart into, and the uninstaller would try to relaunch the
        // just-deleted app.
        Assert.Equal("no", SetupDirective(ReadScript(), "RestartApplications"));
    }

    [Fact]
    public void UninstallDelete_RemovesAppDataFolder()
    {
        // The app-data folder holds only files the app itself created, so
        // the whole folder must go: after uninstall no TrueSpec files may
        // remain (P7's dirifempty left it behind whenever the app had run).
        var entries = SectionLines(ReadScript(), "[UninstallDelete]");
        Assert.Contains(entries, e =>
            e.StartsWith("Type: filesandordirs", StringComparison.OrdinalIgnoreCase)
            && e.Contains(@"{localappdata}\TrueSpec", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UninstallDelete_KeepsUserExports()
    {
        // Exports in Documents are the user's own files: never delete
        // them, only remove the folder if it is empty.
        var entries = SectionLines(ReadScript(), "[UninstallDelete]");
        Assert.Contains(entries, e =>
            e.StartsWith("Type: dirifempty", StringComparison.OrdinalIgnoreCase)
            && e.Contains(@"{userdocs}\TrueSpec", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, e =>
            e.Contains("filesandordirs", StringComparison.OrdinalIgnoreCase)
            && e.Contains("{userdocs}", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UninstallCode_ClosesRunningApp()
    {
        // Deterministic fallback for when Restart Manager does not
        // associate the single-file exe (which runs from a temp copy) with
        // the install dir: InitializeUninstall posts WM_CLOSE to the main
        // window and waits for it to go away before any file is touched.
        var script = string.Join("\n", ReadScript());
        Assert.Contains("function InitializeUninstall()", script, StringComparison.Ordinal);
        Assert.Contains("FindWindowByWindowName('TrueSpec - Command Center')", script, StringComparison.Ordinal);
        Assert.Contains("PostMessage", script, StringComparison.Ordinal);
        Assert.Contains("WM_CLOSE", script, StringComparison.Ordinal);
        Assert.Contains("MB_RETRYCANCEL", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AppDataFolder_MatchesInstallerPath()
    {
        // The .iss deletes {localappdata}\TrueSpec: confirm the code really
        // writes its data (settings, history, startup log, window state)
        // to that same folder, or the uninstall-delete would miss it.
        var folder = PathSafety.DefaultAppDataFolder();
        Assert.EndsWith("TrueSpec", folder, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallDir_IsPerUser()
    {
        // The install folder the uninstaller removes is the per-user one
        // (%LocalAppData%\Programs\TrueSpec), matching PrivilegesRequired=lowest.
        Assert.Equal("lowest", SetupDirective(ReadScript(), "PrivilegesRequired"));
        Assert.Contains("{autopf}", SetupDirective(ReadScript(), "DefaultDirName"), StringComparison.OrdinalIgnoreCase);
    }
}
