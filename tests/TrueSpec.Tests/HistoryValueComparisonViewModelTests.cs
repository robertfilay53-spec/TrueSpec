using TrueSpec.App.ViewModels;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

/// <summary>
/// U9 gating fix: the value-comparison input section in HistoryView is now
/// always visible, even with a single-machine history, so the ViewModel
/// state it binds to must already be meaningful before any machine
/// comparison exists: the prompt text is shown, the Compare value button
/// is disabled, and no verdict is shown. Windows leg only (it drives
/// TrueSpec.App's ViewModel), like HistoryForecastViewModelTests:
/// excluded from the net10.0 leg in the csproj.
/// </summary>
public class HistoryValueComparisonViewModelTests
{
    [Fact]
    public void NoMachineComparison_ValueSectionIsPromptNotVerdict()
    {
        var current = new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Test Laptop",
            "Windows 11 Pro",
            Array.Empty<ComponentSnapshot>(),
            ScoreResult.Empty,
            new BuyerCheckResult(Array.Empty<BuyerCheck>()));

        var viewModel = new HistoryViewModel(store: null, current);

        // Nothing compared yet: the always-visible U9 section shows its
        // entry prompt, not a verdict, and the button stays disabled
        // until two machines are compared.
        Assert.False(viewModel.HasMachineComparison);
        Assert.False(viewModel.CanCompareValue);
        Assert.False(viewModel.CompareValueCommand.CanExecute(null));
        Assert.False(viewModel.HasValueComparison);
        Assert.Equal(
            "Enter each seller's asking price to compare value.",
            viewModel.ValueStatusText);
        Assert.Equal(string.Empty, viewModel.ValueVerdictText);
        Assert.Equal(string.Empty, viewModel.MachineAName);
        Assert.Equal(string.Empty, viewModel.MachineBName);
        Assert.Equal(string.Empty, viewModel.PriceAText);
        Assert.Equal(string.Empty, viewModel.PriceBText);
    }
}
