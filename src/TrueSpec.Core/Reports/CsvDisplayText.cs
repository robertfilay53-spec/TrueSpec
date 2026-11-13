namespace TrueSpec.Core.Reports;

using System.Globalization;

/// <summary>
/// The shared text for values the CSV exporters write, so the files agree
/// with the tables exactly: a score reads "Not measurable" when null else
/// "0.#", a percent reads "Not reported" when null else "0.#%". One
/// definition, used by the Fleet and per-machine exporters (the History
/// exporter still has its own private copies, predating this helper).
/// </summary>
internal static class CsvDisplayText
{
    internal static string ScoreText(double? score) =>
        score is null ? "Not measurable" : score.Value.ToString("0.#", CultureInfo.InvariantCulture);

    internal static string PercentText(double? value) =>
        value is null
            ? "Not reported"
            : string.Create(CultureInfo.InvariantCulture, $"{value.Value:0.#}%");
}
