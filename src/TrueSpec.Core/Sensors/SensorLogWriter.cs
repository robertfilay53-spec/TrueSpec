using System.Globalization;

namespace TrueSpec.Core.Sensors;

/// <summary>
/// Appends live sensor snapshots to a local CSV log under
/// %LocalAppData%\TrueSpec. One row per sample, one column per sensor
/// reading. A sensor that stops reporting leaves its cell empty: a gap is
/// a gap, never a zero and never interpolated.
///
/// The file can never grow unbounded: at most <see cref="DefaultMaxRows"/>
/// rows are kept, and when the cap is crossed the file is rewritten keeping
/// the newest <see cref="DefaultTrimToRows"/> rows. At the default 2s
/// refresh rate 10,000 rows is about 5.5 hours of history and roughly 2 MB.
/// The columns are fixed by the first sample that has readings, so the file
/// stays append-only; a sensor that first appears later is ignored.
/// </summary>
public sealed class SensorLogWriter
{
    public const string DefaultFileName = "sensor-log.csv";

    /// <summary>Hard cap on data rows (excluding the header).</summary>
    public const int DefaultMaxRows = 10000;

    /// <summary>Rows kept when the cap fires. Below the cap so the trim does not run on every sample.</summary>
    public const int DefaultTrimToRows = 8000;

    private const string TimestampHeader = "Timestamp";

    private readonly string _filePath;
    private readonly int _maxRows;
    private readonly int _trimToRows;
    private readonly object _gate = new();

    private List<string>? _columns;
    private int _rowCount = -1;

    public SensorLogWriter(string filePath, int maxRows = DefaultMaxRows, int trimToRows = DefaultTrimToRows)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A sensor log path is required.", nameof(filePath));
        }

        if (maxRows < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRows), "The row cap must be at least 1.");
        }

        if (trimToRows < 1 || trimToRows >= maxRows)
        {
            throw new ArgumentOutOfRangeException(nameof(trimToRows), "The trim target must be at least 1 and below the row cap.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("The sensor log path is not valid.", nameof(filePath), ex);
        }

        _filePath = fullPath;
        _maxRows = maxRows;
        _trimToRows = trimToRows;
    }

    /// <summary>The default log path: %LocalAppData%\TrueSpec\sensor-log.csv.</summary>
    public static string DefaultFilePath() =>
        Path.Combine(Security.PathSafety.DefaultAppDataFolder(), DefaultFileName);

    /// <summary>
    /// Builds the writer for the default path, or null when the path fails
    /// validation. Logging is a convenience: it must never block startup.
    /// </summary>
    public static SensorLogWriter? CreateDefault()
    {
        var path = Security.PathSafety.ValidateSensorLogPath(
            DefaultFilePath(),
            Security.PathSafety.DefaultAppDataFolder());
        if (path is null)
        {
            return null;
        }

        try
        {
            return new SensorLogWriter(path);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public string FilePath => _filePath;

    /// <summary>False while the user has logging switched off: Append is a no-op.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The last write failure, if any. Logging failures are never thrown.</summary>
    public string? LastError { get; private set; }

    /// <summary>Data rows currently in the file (excluding the header).</summary>
    public int RowCount
    {
        get
        {
            lock (_gate)
            {
                EnsureHeaderLoadedLocked();
                return Math.Max(0, _rowCount);
            }
        }
    }

    /// <summary>
    /// Appends one row for <paramref name="snapshot"/>. Returns false (and
    /// sets <see cref="LastError"/>) when the write failed; logging must
    /// never throw into the sensor refresh loop. A snapshot with no
    /// readings still writes a timestamp row with empty cells, so a dead
    /// sweep is visible in the log as a gap instead of silently vanishing.
    /// </summary>
    public bool Append(SensorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!Enabled)
        {
            return true;
        }

        lock (_gate)
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                EnsureHeaderLoadedLocked();

                if (_columns is null && snapshot.Readings.Count > 0)
                {
                    _columns = BuildColumns(snapshot.Readings);
                    WriteHeaderPreservingGapRowsLocked();
                }

                var row = BuildRow(snapshot);
                File.AppendAllText(_filePath, row + Environment.NewLine);
                _rowCount++;

                if (_rowCount > _maxRows)
                {
                    TrimLocked();
                }

                LastError = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                LastError = ex.Message;
                return false;
            }
        }
    }

    private string HeaderLine() => string.Join(",", new[] { TimestampHeader }.Concat(_columns!));

    /// <summary>
    /// Writes the header for the first time, keeping the timestamp-only
    /// rows written before any reading-bearing snapshot arrived. Those gap
    /// rows used to be destroyed by an unconditional overwrite: a dead
    /// sweep at startup (no sensors reporting yet) vanished the moment the
    /// first real reading fixed the columns. The kept rows are padded
    /// with empty cells so they stay aligned with the new columns.
    /// </summary>
    private void WriteHeaderPreservingGapRowsLocked()
    {
        var existing = File.Exists(_filePath)
            ? File.ReadAllLines(_filePath)
            : Array.Empty<string>();
        var lines = new List<string> { HeaderLine() };
        var preserved = 0;
        foreach (var line in existing)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // A timestamp-only row has no comma; pad it with one empty cell
            // per column. Anything else is kept verbatim.
            lines.Add(line.Contains(',') ? line : line + new string(',', _columns!.Count));
            preserved++;
        }

        File.WriteAllLines(_filePath, lines);
        _rowCount = preserved;
    }

    private string BuildRow(SensorSnapshot snapshot)
    {
        var timestamp = snapshot.CapturedAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
        if (_columns is null || _columns.Count == 0)
        {
            return timestamp;
        }

        var values = new string[_columns.Count];
        foreach (var reading in snapshot.Readings)
        {
            var column = ColumnName(reading);
            var index = _columns.IndexOf(column);
            if (index < 0)
            {
                // A sensor that first appeared after the header was fixed.
                continue;
            }

            // A reading with no usable value leaves its cell empty: a gap
            // is a gap, never a zero. A clock or temperature of zero is the
            // sensor library's "not reporting" sentinel, so it is a gap;
            // a 0 RPM fan is a real reading and still writes "0".
            values[index] = reading.HasValue ? FormatValue(reading.Value) : string.Empty;
        }

        return timestamp + "," + string.Join(",", values);
    }

    private static string FormatValue(double? value) =>
        value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)
            ? string.Empty
            : value.Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Keeps the header and the newest <see cref="_trimToRows"/> rows. Writes
    /// through a temp file in the same folder so a crash mid-trim never
    /// leaves a half-written log behind.
    /// </summary>
    private void TrimLocked()
    {
        var lines = File.ReadAllLines(_filePath);
        if (lines.Length <= _trimToRows + 1)
        {
            _rowCount = Math.Max(0, lines.Length - 1);
            return;
        }

        var kept = new List<string>(lines.Length - (lines.Length - 1 - _trimToRows));
        kept.Add(lines[0]);
        kept.AddRange(lines.Skip(lines.Length - _trimToRows));

        // A GUID temp name, like AppSettingsStore: a predictable ".tmp"
        // name lets another process on the machine guess the path and
        // swap the file between the write and the move.
        var directory = Path.GetDirectoryName(_filePath) ?? ".";
        var tempPath = Path.Combine(directory, $".truespec-{Guid.NewGuid():N}.tmp");
        File.WriteAllLines(tempPath, kept);
        File.Move(tempPath, _filePath, overwrite: true);
        _rowCount = _trimToRows;
    }

    /// <summary>
    /// Reads the existing header (if any) so a new session appends to the
    /// same columns, and counts the existing rows for the cap. Runs once,
    /// under the lock.
    /// </summary>
    private void EnsureHeaderLoadedLocked()
    {
        if (_rowCount >= 0)
        {
            return;
        }

        _rowCount = 0;
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            using var reader = new StreamReader(_filePath);
            var header = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(header))
            {
                return;
            }

            var parts = header.Split(',');
            if (parts.Length < 1 || !string.Equals(parts[0], TimestampHeader, StringComparison.Ordinal))
            {
                return;
            }

            _columns = parts.Skip(1).Where(c => c.Length > 0).ToList();
            var count = 0;
            while (reader.ReadLine() is not null)
            {
                count++;
            }

            _rowCount = count;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A damaged log is not fatal: the next append rebuilds the
            // header from the live snapshot and overwrites it.
            _columns = null;
            _rowCount = 0;
            LastError = ex.Message;
        }
    }

    private static List<string> BuildColumns(IReadOnlyList<SensorReading> readings)
    {
        var columns = new List<string>();
        foreach (var reading in readings)
        {
            var name = ColumnName(reading);
            if (columns.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            columns.Add(name);
        }

        return columns;
    }

    /// <summary>
    /// One stable column per sensor: hardware, name and unit, with anything
    /// that is not a letter, digit or underscore stripped. Two sensors that
    /// sanitize to the same name share one column.
    /// </summary>
    public static string ColumnName(SensorReading reading)
    {
        var hardware = Sanitize(reading.Hardware);
        var name = Sanitize(reading.Name);
        var unit = UnitToken(reading.Unit);
        var column = string.IsNullOrEmpty(unit)
            ? $"{hardware}_{name}"
            : $"{hardware}_{name}_{unit}";
        return column.Length == 0 ? "Sensor" : column;
    }

    private static string Sanitize(string text)
    {
        // Spaces become underscores ("GPU Core" -> "GPU_Core"); anything
        // else that is not a letter, digit or underscore is dropped
        // ("Core #1" -> "Core_1"). Runs of underscores collapse to one.
        var builder = new System.Text.StringBuilder(text.Length);
        var lastWasUnderscore = true;
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasUnderscore = false;
            }
            else if ((char.IsWhiteSpace(c) || c == '_') && !lastWasUnderscore)
            {
                builder.Append('_');
                lastWasUnderscore = true;
            }
        }

        var sanitized = builder.ToString().TrimEnd('_');
        return sanitized;
    }

    private static string UnitToken(string unit) => unit switch
    {
        "%" => "pct",
        "C" => "C",
        "RPM" => "RPM",
        "V" => "V",
        "W" => "W",
        "MHz" => "MHz",
        _ => Sanitize(unit),
    };
}
