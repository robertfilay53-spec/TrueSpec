using System.Globalization;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// Localisation phase 7: the History page's remaining Core-owned wordings
/// in German — trend summary, battery alert, driver-age summary,
/// degradation-forecast basis texts and step-change notes, and the value
/// no-verdict reasons. Every test pins two things: the German wording, and
/// that the no-language and "en" calls still produce the exact English
/// wording the older tests already pin (byte-identical, not just similar).
/// The U9 verdict sentence itself is deliberately NOT translated: it stays
/// English in every language, and one test pins that too.
/// </summary>
public class HistoryCoreWordingsTests
{
    private static readonly DateTimeOffset FirstScan = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastScan = new(2026, 2, 10, 9, 0, 0, TimeSpan.Zero);

    /// <summary>TrendPoint factory: (day offset from a fixed start, value or gap).</summary>
    private static IReadOnlyList<TrendPoint> Points(params (int Day, double? Value)[] points)
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        return points.Select(p => new TrendPoint(start.AddDays(p.Day), p.Value)).ToList();
    }

    private static SystemSnapshot Snapshot(
        double score,
        string batteryHealth,
        string wear,
        DateTimeOffset capturedAt,
        string? oldestDriverDate = null,
        string? driversOlderThan5 = null) =>
        TestSnapshots.HistorySnapshot(
            score, batteryHealth, "10", wear, "100",
            capturedAt, "Test Laptop",
            oldestDriverDate: oldestDriverDate,
            driversOlderThan5: driversOlderThan5);

    /// <summary>
    /// Whole days between a day-precision driver date and the scan date,
    /// computed independently of the implementation (DateOnly day numbers).
    /// </summary>
    private static double AgeDays(DateTimeOffset scan, string driverDate) =>
        new DateOnly(scan.Year, scan.Month, scan.Day).DayNumber
        - DateOnly.ParseExact(driverDate, "yyyy-MM-dd", CultureInfo.InvariantCulture).DayNumber;

    private static string Format(double value) =>
        value.ToString("0.#", CultureInfo.InvariantCulture);

    [Fact]
    public void Analyze_German_SummaryIsGerman()
    {
        var before = Snapshot(90d, "90.0%", "2%", FirstScan);
        var after = Snapshot(85d, "85.5%", "5%", LastScan);

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after }, "de");

        Assert.Equal(2, trend.SnapshotCount);
        Assert.Equal(
            "2 Scans von 2026-01-10 bis 2026-02-10. Punktzahl 90 bis 85 (um 5 Punkte gefallen). Akkugesundheit 90 bis 85.5 (um 4.5 Punkte gefallen). Speicherverschleiß 2 bis 5 (um 3 Punkte gestiegen).",
            trend.Summary);
    }

    [Fact]
    public void Analyze_English_SummaryIsUnchanged()
    {
        var before = Snapshot(90d, "90.0%", "2%", FirstScan);
        var after = Snapshot(85d, "85.5%", "5%", LastScan);

        var withoutLanguage = HistoryTrendAnalyzer.Analyze(new[] { before, after });
        var withEnglish = HistoryTrendAnalyzer.Analyze(new[] { before, after }, "en");

        Assert.Equal(withoutLanguage.Summary, withEnglish.Summary);
        Assert.Equal(
            "2 scans from 2026-01-10 to 2026-02-10. Score 90 to 85 (down 5 points). Battery health 90 to 85.5 (down 4.5 points). Storage wear 2 to 5 (up 3 points).",
            withoutLanguage.Summary);
    }

    [Fact]
    public void Analyze_German_EmptySummaryIsGerman()
    {
        var trend = HistoryTrendAnalyzer.Analyze(Array.Empty<SystemSnapshot>(), "de");

        Assert.Equal(0, trend.SnapshotCount);
        Assert.Equal(
            "Noch keine Scans gespeichert. Führen Sie einen Scan aus und kommen Sie zurück: Jeder Scan wird im lokalen Verlauf gespeichert, und der Verlauf erscheint, sobald es zwei Scans zum Vergleichen gibt.",
            trend.Summary);
    }

    [Fact]
    public void Analyze_German_BatteryAlertIsGerman()
    {
        var before = Snapshot(90d, "90.0%", "2%", FirstScan);
        var after = Snapshot(85d, "70.0%", "5%", LastScan);

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after }, "de");

        Assert.Equal(
            "Die Akkugesundheit ist seit dem ersten gespeicherten Scan von Normal (90 %) auf Abgenutzt (70 %) gefallen. Die Akkugesundheit ist zwischen den letzten beiden Scans, die sie gemeldet haben, um 20 Punkte gefallen (90 % auf 70 %).",
            trend.BatteryAlert);
    }

    [Fact]
    public void Analyze_English_BatteryAlertIsUnchanged()
    {
        var before = Snapshot(90d, "90.0%", "2%", FirstScan);
        var after = Snapshot(85d, "70.0%", "5%", LastScan);

        var withoutLanguage = HistoryTrendAnalyzer.Analyze(new[] { before, after });
        var withEnglish = HistoryTrendAnalyzer.Analyze(new[] { before, after }, "en");

        Assert.Equal(withoutLanguage.BatteryAlert, withEnglish.BatteryAlert);
        Assert.Equal(
            "Battery health dropped from Normal (90%) to Worn (70%) since the first saved scan. Battery health fell 20 points between the last two scans that reported it (90% to 70%).",
            withoutLanguage.BatteryAlert);
    }

    [Fact]
    public void Analyze_German_DriverAgeSummaryIsGerman()
    {
        var before = Snapshot(90d, "90.0%", "2%", FirstScan, "2020-01-15", "3");
        var after = Snapshot(85d, "85.5%", "5%", LastScan, "2020-01-15", "3");

        var trend = HistoryTrendAnalyzer.Analyze(new[] { before, after }, "de");

        Assert.Equal(
            $"Ältester Treiber vom 2020-01-15 ({Format(AgeDays(FirstScan, "2020-01-15"))} Tage alt) auf dem ersten gespeicherten Scan und vom 2020-01-15 ({Format(AgeDays(LastScan, "2020-01-15"))} Tage alt) auf dem neuesten Scan. Der älteste Treiber wurde 31 Tage älter, und die Zahl der Treiber, die älter als 5 Jahre sind, blieb bei 3.",
            trend.DriverAgeSummary);
    }

    [Fact]
    public void Analyze_German_SingleScanDriverAgeIsGerman()
    {
        var only = Snapshot(90d, "90.0%", "2%", FirstScan, "2020-01-15", "3");

        var trend = HistoryTrendAnalyzer.Analyze(new[] { only }, "de");

        Assert.Equal(
            $"Ältester Treiber vom 2020-01-15 ({Format(AgeDays(FirstScan, "2020-01-15"))} Tage alt) auf dem einzigen gespeicherten Scan, der ihn gemeldet hat: Ein weiterer Scan ist für einen Verlauf nötig. Treiber, die älter als 5 Jahre sind: 3 auf diesem Scan.",
            trend.DriverAgeSummary);
    }

    [Fact]
    public void Analyze_English_DriverAgeSummaryIsUnchanged()
    {
        var before = Snapshot(90d, "90.0%", "2%", FirstScan, "2020-01-15", "3");
        var after = Snapshot(85d, "85.5%", "5%", LastScan, "2020-01-15", "3");

        var withoutLanguage = HistoryTrendAnalyzer.Analyze(new[] { before, after });
        var withEnglish = HistoryTrendAnalyzer.Analyze(new[] { before, after }, "en");

        Assert.Equal(withoutLanguage.DriverAgeSummary, withEnglish.DriverAgeSummary);
        Assert.Equal(
            $"Oldest driver dated 2020-01-15 ({Format(AgeDays(FirstScan, "2020-01-15"))} days old) on the first saved scan and 2020-01-15 ({Format(AgeDays(LastScan, "2020-01-15"))} days old) on the latest scan. The oldest driver got 31 days older, and drivers older than 5 years stayed at 3.",
            withoutLanguage.DriverAgeSummary);
    }

    [Fact]
    public void Forecast_German_NotEnoughDataIsGerman()
    {
        var forecast = DegradationForecaster.ForBattery(Points((0, 90d), (60, 88d)), "de");

        Assert.Equal(ForecastStatus.NotEnoughData, forecast.Status);
        Assert.Equal(
            "Akkugesundheit hat 2 Scans über 60 Tage. Eine Prognose braucht mindestens 3 Scans über mindestens 30 Tage: 1 weiterer Scan nötig.",
            forecast.BasisText);
    }

    [Fact]
    public void Forecast_German_NotEnoughDaysIsGerman()
    {
        var forecast = DegradationForecaster.ForBattery(Points((0, 90d), (14, 89d), (29, 88d)), "de");

        Assert.Equal(ForecastStatus.NotEnoughData, forecast.Status);
        Assert.Equal(
            "Akkugesundheit hat 3 Scans über 29 Tage. Eine Prognose braucht mindestens 3 Scans über mindestens 30 Tage: 1 weiterer Tag Verlauf nötig.",
            forecast.BasisText);
    }

    [Fact]
    public void Forecast_English_NotEnoughDataIsUnchanged()
    {
        var points = Points((0, 90d), (60, 88d));

        var withoutLanguage = DegradationForecaster.ForBattery(points);
        var withEnglish = DegradationForecaster.ForBattery(points, "en");

        Assert.Equal(withoutLanguage.BasisText, withEnglish.BasisText);
        Assert.Equal(
            "Battery health has 2 scans over 60 days. A forecast needs at least 3 scans over at least 30 days: 1 more scan needed.",
            withoutLanguage.BasisText);
    }

    [Fact]
    public void Forecast_German_ReadyBatteryIsGerman()
    {
        var forecast = DegradationForecaster.ForBattery(
            Points((0, 90d), (30, 89d), (60, 88d), (90, 87d)), "de");

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(23, forecast.ForecastLowMonths);
        Assert.Equal(30, forecast.ForecastHighMonths);
        Assert.Equal(
            "Akkugesundheit ist um 3 Punkte über 4 Scans (90 Tage) gefallen. Bei dieser gemessenen Rate wird Austausch-Schwelle (Akkugesundheit unter 60 %) in 23 bis 30 Monaten erreicht.",
            forecast.BasisText);
    }

    [Fact]
    public void Forecast_German_ReadyStorageIsGerman()
    {
        var forecast = DegradationForecaster.ForStorageWear(
            Points((0, 2d), (30, 4d), (60, 6d), (90, 8d)), "de");

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(14, forecast.ForecastLowMonths);
        Assert.Equal(18, forecast.ForecastHighMonths);
        Assert.Equal(
            "SSD-Verschleiß ist um 6 Punkte über 4 Scans (90 Tage) gestiegen. Bei dieser gemessenen Rate wird Austausch-Schwelle (Speicherverschleiß über 40 %) in 14 bis 18 Monaten erreicht.",
            forecast.BasisText);
    }

    [Fact]
    public void Forecast_English_ReadyBatteryIsUnchanged()
    {
        var points = Points((0, 90d), (30, 89d), (60, 88d), (90, 87d));

        var withoutLanguage = DegradationForecaster.ForBattery(points);
        var withEnglish = DegradationForecaster.ForBattery(points, "en");

        Assert.Equal(withoutLanguage.BasisText, withEnglish.BasisText);
        Assert.Equal(
            "Battery health fell 3 points over 4 scans (90 days). At that measured rate, it reaches the Replace threshold (battery health below 60%) in 23 to 30 months.",
            withoutLanguage.BasisText);
    }

    [Fact]
    public void Forecast_German_NoDeclineIsGerman()
    {
        var forecast = DegradationForecaster.ForBattery(
            Points((0, 90d), (30, 90d), (60, 90d), (90, 90d)), "de");

        Assert.Equal(ForecastStatus.NoMeasurableDecline, forecast.Status);
        Assert.Equal(
            "Akkugesundheit zeigte über 4 Scans (90 Tage) keinen messbaren Rückgang: 90 % auf 90 %. Keine Prognose, bis ein Rückgang gemessen wird.",
            forecast.BasisText);
    }

    [Fact]
    public void Forecast_German_AlreadyReachedIsGerman()
    {
        var forecast = DegradationForecaster.ForBattery(
            Points((0, 59d), (30, 58d), (60, 57d), (90, 56d)), "de");

        // Already past the threshold is still a Ready forecast, with a
        // 0-to-0 month range: the threshold is reached, not "in 0 months".
        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(0, forecast.ForecastLowMonths);
        Assert.Equal(0, forecast.ForecastHighMonths);
        Assert.Equal(
            "Akkugesundheit hat Austausch-Schwelle (Akkugesundheit unter 60 %) bereits erreicht: 56 % beim neuesten Scan (4 Scans, 90 Tage).",
            forecast.BasisText);
    }

    [Fact]
    public void Forecast_German_BatteryStepChangeIsGerman()
    {
        var forecast = DegradationForecaster.ForBattery(
            Points((0, 70d), (30, 68d), (60, 96d), (90, 95d), (120, 94d)), "de");

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(
            "Akkugesundheit sprang von 68 % auf 96 % zwischen den Scans, also der Akku sieht ausgetauscht aus; die Prognose startet hier neu.",
            forecast.StepChangeNote);
        Assert.StartsWith(forecast.StepChangeNote + " ", forecast.BasisText, StringComparison.Ordinal);
    }

    [Fact]
    public void Forecast_German_StorageStepChangeIsGerman()
    {
        var forecast = DegradationForecaster.ForStorageWear(
            Points((0, 30d), (30, 32d), (60, 2d), (90, 3d), (120, 4d)), "de");

        Assert.Equal(ForecastStatus.Ready, forecast.Status);
        Assert.Equal(
            "SSD-Verschleiß sank von 32 % auf 2 % zwischen den Scans, also das Laufwerk sieht ausgetauscht aus; die Prognose startet hier neu.",
            forecast.StepChangeNote);
    }

    [Fact]
    public void Forecast_English_StepChangeIsUnchanged()
    {
        var points = Points((0, 70d), (30, 68d), (60, 96d), (90, 95d), (120, 94d));

        var withoutLanguage = DegradationForecaster.ForBattery(points);
        var withEnglish = DegradationForecaster.ForBattery(points, "en");

        Assert.Equal(withoutLanguage.StepChangeNote, withEnglish.StepChangeNote);
        Assert.Equal(
            "Battery health jumped from 68% to 96% between scans, so the battery looks replaced; forecast restarts here.",
            withoutLanguage.StepChangeNote);
    }

    [Fact]
    public void ValueCompare_German_NoScoreReasonIsGerman()
    {
        var comparison = new MachineComparison(
            new MachineSideSummary("Machine A", 2, null, null, null, null, null, null, null),
            new MachineSideSummary("Machine B", 2, null, null, 82d, null, null, null, null));

        var result = ValueComparer.Compare(comparison, 500m, 500m, languageCode: "de");

        Assert.Null(result.VerdictSentence);
        Assert.Equal(
            "Für eine Kaufempfehlung braucht es eine gemessene Gesundheitsbewertung für beide Geräte.",
            result.NoVerdictReason);
    }

    [Fact]
    public void ValueCompare_German_NoPriceReasonIsGerman()
    {
        var comparison = new MachineComparison(
            new MachineSideSummary("Machine A", 2, null, null, 71d, null, null, null, null),
            new MachineSideSummary("Machine B", 2, null, null, 82d, null, null, null, null));

        var result = ValueComparer.Compare(comparison, 500m, null, languageCode: "de");

        Assert.Null(result.VerdictSentence);
        Assert.Equal(
            "Geben Sie für jedes Gerät einen Angebotspreis über null ein, um den Wert zu vergleichen.",
            result.NoVerdictReason);
    }

    [Fact]
    public void ValueCompare_German_VerdictSentenceStaysEnglish()
    {
        var comparison = new MachineComparison(
            new MachineSideSummary("Machine A", 2, null, null, 71d, null, null, null, null),
            new MachineSideSummary("Machine B", 2, null, null, 82d, null, null, null, null));

        var german = ValueComparer.Compare(comparison, 620m, 500m, languageCode: "de");
        var english = ValueComparer.Compare(comparison, 620m, 500m);

        Assert.Equal(english.VerdictSentence, german.VerdictSentence);
        Assert.Equal(
            "Machine B scores 82 vs 71 and costs $120 less. On measured health per dollar, B is the better buy.",
            german.VerdictSentence);
    }

    [Fact]
    public void ValueCompare_English_NoVerdictReasonsAreUnchanged()
    {
        var noScore = new MachineComparison(
            new MachineSideSummary("Machine A", 2, null, null, null, null, null, null, null),
            new MachineSideSummary("Machine B", 2, null, null, 82d, null, null, null, null));
        var noPrice = new MachineComparison(
            new MachineSideSummary("Machine A", 2, null, null, 71d, null, null, null, null),
            new MachineSideSummary("Machine B", 2, null, null, 82d, null, null, null, null));

        Assert.Equal(
            "A value verdict needs a measured health score for both machines.",
            ValueComparer.Compare(noScore, 500m, 500m).NoVerdictReason);
        Assert.Equal(
            "Enter an asking price above zero for each machine to compare value.",
            ValueComparer.Compare(noPrice, 500m, null).NoVerdictReason);
    }
}
