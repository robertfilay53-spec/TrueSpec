using System.Globalization;

namespace TrueSpec.Core.Sensors;

/// <summary>One logged sample for one column: a value, or null when the sensor was not reporting.</summary>
public sealed record SensorLogPoint(DateTimeOffset Timestamp, double? Value);

/// <summary>One column read back from the sensor log.</summary>
public sealed record SensorLogSeries(
    IReadOnlyList<string> Columns,
    IReadOnlyList<SensorLogPoint> Points,
    int RowCount,
    int GapCount,
    DateTimeOffset? FirstAt,
    DateTimeOffset? LastAt);

/// <summary>
/// Reads the U6 sensor log back for the History chart. A missing file is an
/// empty series, not an error. A line with an unparseable timestamp is
/// skipped; a cell that is empty or unparseable is a gap in the series,
/// never a zero.
/// </summary>
public static class SensorLogReader
{
    /// <summary>Reads one column of the log. <paramref name="columnName"/> must match the header exactly.</summary>
    public static SensorLogSeries Read(string filePath, string columnName)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(columnName);

        var columns = ReadColumns(filePath);
        var columnIndex = -1;
        for (var i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i], columnName, StringComparison.Ordinal))
            {
                columnIndex = i;
                break;
            }
        }
        var points = new List<SensorLogPoint>();
        var rowCount = 0;
        var gapCount = 0;

        if (columns.Count == 0)
        {
            return new SensorLogSeries(columns, points, 0, 0, null, null);
        }

        try
        {
            using var reader = new StreamReader(filePath);
            _ = reader.ReadLine();
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var fields = SplitCsvLine(line);
                if (fields.Count == 0 ||
                    !DateTimeOffset.TryParse(fields[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
                {
                    continue;
                }

                rowCount++;
                double? value = null;
                if (columnIndex >= 0 && fields.Count > columnIndex + 1 &&
                    double.TryParse(fields[columnIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    value = parsed;
                }
                else
                {
                    gapCount++;
                }

                points.Add(new SensorLogPoint(timestamp, value));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A log that cannot be read is an empty series, not a crash.
            return new SensorLogSeries(columns, points, rowCount, gapCount, FirstOf(points), LastOf(points));
        }

        return new SensorLogSeries(columns, points, rowCount, gapCount, FirstOf(points), LastOf(points));
    }

    /// <summary>Returns the header columns, or an empty list when the file is missing or has no header.</summary>
    public static IReadOnlyList<string> ReadColumns(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return Array.Empty<string>();
            }

            using var reader = new StreamReader(filePath);
            var header = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(header))
            {
                return Array.Empty<string>();
            }

            var parts = header.Split(',');
            if (parts.Length < 1 || !string.Equals(parts[0], "Timestamp", StringComparison.Ordinal))
            {
                return Array.Empty<string>();
            }

            return parts.Skip(1).Where(c => c.Length > 0).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Picks the CPU temperature column for the History chart: the first
    /// CPU column whose sensor is a package or core temperature. A CPU load
    /// column is never a temperature, so it is not picked.
    /// </summary>
    public static string? FindCpuTemperatureColumn(IReadOnlyList<string> columns)
    {
        foreach (var column in columns)
        {
            if (!column.StartsWith("CPU_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!column.EndsWith("_C", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (column.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                column.Contains("Core", StringComparison.OrdinalIgnoreCase))
            {
                return column;
            }
        }

        return null;
    }

    private static DateTimeOffset? FirstOf(IReadOnlyList<SensorLogPoint> points) =>
        points.Count == 0 ? null : points[0].Timestamp;

    private static DateTimeOffset? LastOf(IReadOnlyList<SensorLogPoint> points) =>
        points.Count == 0 ? null : points[^1].Timestamp;

    /// <summary>Minimal RFC 4180 split: quoted fields may contain commas and doubled quotes.</summary>
    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
