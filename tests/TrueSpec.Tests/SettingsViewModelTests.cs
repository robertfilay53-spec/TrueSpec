using TrueSpec.App.ViewModels;
using TrueSpec.Core;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Sensors;
using TrueSpec.Core.Security;

namespace TrueSpec.Tests;

/// <summary>
/// Settings page, data-location section (v2.1.1): the portable-mode
/// status names the folder the app keeps its data in, and the toggle
/// creates or removes the marker file next to the exe. Windows leg only
/// (it drives TrueSpec.App's ViewModel), like the other ViewModel tests:
/// excluded from the net10.0 leg in the csproj.
/// </summary>
public class SettingsViewModelTests
{
    private static string TempExeDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static SettingsViewModel ViewModelFor(string exeDir, string? environmentValue = null)
    {
        var main = new MainViewModel(
            MachineScanService.CreateDefault(),
            new UnavailableSensorMonitor(),
            new BuyerCheckEvaluator());
        return new SettingsViewModel(main, exeDirectory: exeDir, environmentValue: environmentValue);
    }

    [Fact]
    public void Status_NoMarker_ShowsDefaultFolderAndOff()
    {
        var exeDir = TempExeDir();

        try
        {
            var viewModel = ViewModelFor(exeDir);

            Assert.Equal(PathSafety.DefaultAppDataFolder(), viewModel.DataFolderText);
            Assert.Contains("off", viewModel.PortableModeSummaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("default folder", viewModel.PortableModeSummaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Turn portable mode on", viewModel.PortableModeActionText);
            Assert.Equal(string.Empty, viewModel.PortableModeMessage);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Status_MarkerPresent_ShowsPortableFolderAndOn()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var viewModel = ViewModelFor(exeDir);

            Assert.Equal(Path.GetFullPath(exeDir), viewModel.DataFolderText);
            Assert.Contains("on", viewModel.PortableModeSummaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("next to the app", viewModel.PortableModeSummaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Turn portable mode off", viewModel.PortableModeActionText);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void Status_EnvVar_ShowsEnvNote()
    {
        var exeDir = TempExeDir();

        try
        {
            var viewModel = ViewModelFor(exeDir, environmentValue: "1");

            Assert.Contains("environment variable", viewModel.PortableModeSummaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Path.GetFullPath(exeDir), viewModel.DataFolderText);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void TogglePortableMode_On_CreatesMarkerAndNamesRestart()
    {
        var exeDir = TempExeDir();

        try
        {
            var viewModel = ViewModelFor(exeDir);
            viewModel.TogglePortableModeCommand.Execute(null);

            Assert.True(File.Exists(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName)));
            Assert.Contains("Restart", viewModel.PortableModeMessage, StringComparison.Ordinal);
            Assert.Equal("Turn portable mode off", viewModel.PortableModeActionText);
            Assert.Equal(Path.GetFullPath(exeDir), viewModel.DataFolderText);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void TogglePortableMode_Off_RemovesMarker()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var viewModel = ViewModelFor(exeDir);
            viewModel.TogglePortableModeCommand.Execute(null);

            Assert.False(File.Exists(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName)));
            Assert.Contains("Restart", viewModel.PortableModeMessage, StringComparison.Ordinal);
            Assert.Equal("Turn portable mode on", viewModel.PortableModeActionText);
            Assert.Equal(PathSafety.DefaultAppDataFolder(), viewModel.DataFolderText);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }

    [Fact]
    public void TogglePortableMode_OffWithEnvVar_NamesTheEnvVar()
    {
        var exeDir = TempExeDir();
        File.WriteAllText(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName), string.Empty);

        try
        {
            var viewModel = ViewModelFor(exeDir, environmentValue: "1");
            viewModel.TogglePortableModeCommand.Execute(null);

            Assert.False(File.Exists(Path.Combine(exeDir, AppDataRoot.PortableMarkerFileName)));
            Assert.Contains("TRUESPEC_PORTABLE", viewModel.PortableModeMessage, StringComparison.Ordinal);
            Assert.Equal(Path.GetFullPath(exeDir), viewModel.DataFolderText);
        }
        finally
        {
            Directory.Delete(exeDir, recursive: true);
        }
    }
}
