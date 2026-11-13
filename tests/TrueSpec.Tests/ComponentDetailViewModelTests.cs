using System.Globalization;
using TrueSpec.App.ViewModels;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

namespace TrueSpec.Tests;

/// <summary>
/// Localisation, phase 4 (v2.1.10): the component detail pages in
/// German. The shared detail view is the one surface every component
/// page uses, so one German-seeded view model proves all 15 pages'
/// chrome. The data stays English by design: component names,
/// headlines, health and status texts, spec labels, values and
/// sources, and the "What this means" text are measured data from the
/// collectors, translated in a later slice — the tests pin that the
/// data does not change when the chrome does.
/// </summary>
public sealed class ComponentDetailViewModelTests
{
    private static SystemSnapshot SnapshotWith(params ComponentSnapshot[] components)
    {
        var list = components.ToList();
        return new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Test Laptop",
            "Windows 11 Pro",
            list,
            new ScoringEngine().Score(list),
            new BuyerCheckEvaluator().Evaluate(list, new BuyerCheckContext("16 GB")));
    }

    private static ComponentSnapshot SystemWithDates(string installDate, string biosDate)
    {
        var now = DateTimeOffset.UtcNow;
        return new ComponentSnapshot(
            "system",
            "System",
            "System",
            "system",
            "Test Laptop",
            null,
            new[]
            {
                new SpecValue("Windows install date", installDate, "CIM Win32_OperatingSystem.InstallDate"),
                new SpecValue("BIOS date", biosDate, "CIM Win32_BIOS.ReleaseDate"),
            },
            "System information comes from Windows.",
            now);
    }

    [Fact]
    public void EnglishDefault_RendersChromeInEnglish()
    {
        var viewModel = new ComponentDetailViewModel(TestSnapshots.Healthy(), "cpu");

        Assert.Equal("Specifications", viewModel.SpecificationsTitleText);
        Assert.Equal("Search specifications", viewModel.SearchPlaceholderText);
        Assert.Equal(
            "Click any value to copy it. Hover a value or source name to see where the reading came from.",
            viewModel.CopyHintText);
        Assert.Equal("What this means", viewModel.WhatThisMeansTitleText);
        Assert.StartsWith("Reading captured ", viewModel.CapturedText, StringComparison.Ordinal);
    }

    [Fact]
    public void GermanSeeded_RendersWholePageInGerman()
    {
        var viewModel = new ComponentDetailViewModel(TestSnapshots.Healthy(), "cpu", "de");

        Assert.Equal("Spezifikationen", viewModel.SpecificationsTitleText);
        Assert.Equal("Spezifikationen durchsuchen", viewModel.SearchPlaceholderText);
        Assert.Equal(
            "Klicken Sie auf einen Wert, um ihn zu kopieren. Bewegen Sie die Maus über einen Wert oder Quellennamen, um zu sehen, woher der Messwert stammt.",
            viewModel.CopyHintText);
        Assert.Equal("Was das bedeutet", viewModel.WhatThisMeansTitleText);
        Assert.StartsWith("Messwert erfasst ", viewModel.CapturedText, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyLanguage_SwitchesBothWays()
    {
        var viewModel = new ComponentDetailViewModel(TestSnapshots.Healthy(), "cpu");

        viewModel.ApplyLanguage("de");
        Assert.Equal("Spezifikationen", viewModel.SpecificationsTitleText);
        Assert.Equal("Was das bedeutet", viewModel.WhatThisMeansTitleText);

        viewModel.ApplyLanguage("en");
        Assert.Equal("Specifications", viewModel.SpecificationsTitleText);
        Assert.Equal("What this means", viewModel.WhatThisMeansTitleText);
    }

    [Fact]
    public void UnknownLanguage_FallsBackToEnglish()
    {
        var viewModel = new ComponentDetailViewModel(TestSnapshots.Healthy(), "cpu", "fr");

        Assert.Equal("Specifications", viewModel.SpecificationsTitleText);
        Assert.Equal("What this means", viewModel.WhatThisMeansTitleText);
    }

    [Fact]
    public void DataStaysEnglish_WhenChromeIsGerman()
    {
        var viewModel = new ComponentDetailViewModel(TestSnapshots.Healthy(), "cpu", "de");

        Assert.Equal("CPU", viewModel.Name);
        Assert.Equal("Test CPU", viewModel.Headline);
        Assert.Equal("CPU health uses live temperature.", viewModel.WhatThisMeans);
        var row = Assert.Single(viewModel.Specs);
        Assert.Equal("Package temperature", row.Label);
        Assert.Equal("55 C", row.Value);
        Assert.Equal("LibreHardwareMonitor", row.Source);
    }

    [Fact]
    public void MissingComponent_FallbackStringsAreLocalized()
    {
        var english = new ComponentDetailViewModel(TestSnapshots.Healthy(), "no-such-component");
        Assert.Equal("Not collected", english.Headline);
        Assert.Equal("This component has not been collected yet.", english.WhatThisMeans);
        Assert.Equal("No specifications were collected for this component.", english.NoSpecsMessage);

        var german = new ComponentDetailViewModel(TestSnapshots.Healthy(), "no-such-component", "de");
        Assert.Equal("Nicht erfasst", german.Headline);
        Assert.Equal("Diese Komponente wurde noch nicht erfasst.", german.WhatThisMeans);
        Assert.Equal("Für diese Komponente wurden keine Spezifikationen erfasst.", german.NoSpecsMessage);
    }

    [Fact]
    public void NoSpecsMessage_NoMatch_NamesTheSearchInBothLanguages()
    {
        var english = new ComponentDetailViewModel(TestSnapshots.Healthy(), "cpu");
        english.SearchText = "zzz-no-such-spec";
        Assert.Equal(
            "No specifications match \"zzz-no-such-spec\". Clear the search to see them all.",
            english.NoSpecsMessage);

        var german = new ComponentDetailViewModel(TestSnapshots.Healthy(), "cpu", "de");
        german.SearchText = "zzz-no-such-spec";
        Assert.Equal(
            "Keine Spezifikationen passen zu „zzz-no-such-spec\". Leeren Sie die Suche, um alle zu sehen.",
            german.NoSpecsMessage);
    }

    [Fact]
    public void EstimateSuffix_IsLocalizedWithTheValue()
    {
        var cpu = new ComponentSnapshot(
            "cpu",
            "CPU",
            "CPU",
            "cpu",
            "Test CPU",
            95d,
            new[] { new SpecValue("Package temperature", "55 C", "LibreHardwareMonitor", IsEstimate: true) },
            "CPU health uses live temperature.",
            DateTimeOffset.UtcNow);

        var english = new ComponentDetailViewModel(SnapshotWith(cpu), "cpu");
        Assert.Equal("55 C (ESTIMATE)", Assert.Single(english.Specs).DisplayValue);

        var german = new ComponentDetailViewModel(SnapshotWith(cpu), "cpu", "de");
        Assert.Equal("55 C (geschätzt)", Assert.Single(german.Specs).DisplayValue);

        german.ApplyLanguage("en");
        Assert.Equal("55 C (ESTIMATE)", Assert.Single(german.Specs).DisplayValue);
    }

    [Fact]
    public void AgeSignals_GroupChromeIsLocalized_SystemPageOnly()
    {
        var english = new ComponentDetailViewModel(TestSnapshots.Healthy(), "system");
        Assert.True(english.HasAgeSignals);
        Assert.Equal("Age signals", english.AgeSignalsTitleText);
        Assert.Equal(
            "The raw numbers behind the authenticity verdict. Each signal names where it was read from.",
            english.AgeSignalsIntroText);

        var german = new ComponentDetailViewModel(TestSnapshots.Healthy(), "system", "de");
        Assert.True(german.HasAgeSignals);
        Assert.Equal("Alterssignale", german.AgeSignalsTitleText);
        Assert.Equal(
            "Die Rohwerte hinter dem Echtheitsurteil. Jedes Signal nennt seine Quelle.",
            german.AgeSignalsIntroText);

        var cpu = new ComponentDetailViewModel(TestSnapshots.Healthy(), "cpu", "de");
        Assert.False(cpu.HasAgeSignals);
    }

    [Fact]
    public void AgeSignalsNote_SkippedChecksAreCountedInBothLanguages()
    {
        var bare = new ComponentSnapshot(
            "system",
            "System",
            "System",
            "system",
            "Test Laptop",
            null,
            Array.Empty<SpecValue>(),
            "System information comes from Windows.",
            DateTimeOffset.UtcNow);

        var english = new ComponentDetailViewModel(SnapshotWith(bare), "system");
        Assert.True(english.HasAgeSignalsNote);
        Assert.Contains("authenticity check(s) could not run", english.AgeSignalsNote, StringComparison.Ordinal);

        var german = new ComponentDetailViewModel(SnapshotWith(bare), "system", "de");
        Assert.True(german.HasAgeSignalsNote);
        Assert.Contains("Echtheitsprüfung(en) konnte(n) nicht ausgeführt werden", german.AgeSignalsNote, StringComparison.Ordinal);
    }

    [Fact]
    public void UntrustedSignal_ReliabilityTextIsLocalized()
    {
        // R4: a BIOS date after the Windows install date is impossible
        // data, so the signal is proved unreliable.
        var system = SystemWithDates("2026-11-01", "2026-11-10");
        var german = new ComponentDetailViewModel(SnapshotWith(system), "system", "de");

        var untrusted = Assert.Single(german.AgeSignals, s => !s.IsReliable);
        Assert.True(untrusted.HasReliabilityText);
        Assert.Equal(
            "Nicht vertrauenswürdig: Eine andere Prüfung hat bewiesen, dass dieses Datum unmöglich ist.",
            untrusted.ReliabilityText);

        var english = new ComponentDetailViewModel(SnapshotWith(system), "system");
        var untrustedEnglish = Assert.Single(english.AgeSignals, s => !s.IsReliable);
        Assert.Equal(
            "Not trusted: another check proved this date impossible.",
            untrustedEnglish.ReliabilityText);
    }

    [Fact]
    public void CapturedText_KeepsTheAppDateShapeInGerman()
    {
        var captured = new DateTimeOffset(2026, 11, 12, 10, 30, 0, TimeSpan.Zero);
        var cpu = new ComponentSnapshot(
            "cpu",
            "CPU",
            "CPU",
            "cpu",
            "Test CPU",
            95d,
            new[] { new SpecValue("Package temperature", "55 C", "LibreHardwareMonitor") },
            "CPU health uses live temperature.",
            captured);

        var german = new ComponentDetailViewModel(SnapshotWith(cpu), "cpu", "de");
        Assert.Equal("Messwert erfasst 2026-11-12 10:30:00", german.CapturedText);
    }
}
