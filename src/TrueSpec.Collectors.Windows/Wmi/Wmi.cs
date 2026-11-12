namespace TrueSpec.Collectors.Windows.Wmi;

using System.Management;

/// <summary>
/// All Windows hardware reads go through CIM/WMI. WMIC is never used because it
/// was removed from Windows 11 25H2.
/// </summary>
internal static class Wmi
{
    // A WMI class can return an unbounded number of rows (thousands of
    // PnP drivers, event-heavy classes). Reads stop after this many rows so
    // one chatty class cannot balloon memory or stall the scan; the cut is
    // recorded in `error` so the caller knows the result is partial. Callers
    // that treat any non-null error as "query failed" must check the row
    // count first: truncation with rows is partial data, not a failure.
    public const int MaxRows = 1000;

    public const string TruncatedError = "Query returned more than 1000 rows; results truncated to the first 1000.";

    public static IReadOnlyList<WmiRow> Query(string namespacePath, string query) =>
        Query(namespacePath, query, out _);

    public static IReadOnlyList<WmiRow> Query(string namespacePath, string query, out string? error)
    {
        var rows = new List<WmiRow>();
        error = null;

        try
        {
            var scope = new ManagementScope($@"\\.\{namespacePath}");
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
            using var results = searcher.Get();

            foreach (ManagementBaseObject item in results)
            {
                using (item)
                {
                    var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (PropertyData property in item.Properties)
                    {
                        values[property.Name] = property.Value;
                    }

                    rows.Add(new WmiRow(values));
                }

                if (rows.Count >= MaxRows)
                {
                    error = TruncatedError;
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }

        return rows;
    }

    public static IReadOnlyList<ManagementObject> QueryRaw(
        string namespacePath,
        string query,
        out string? error)
    {
        var rows = new List<ManagementObject>();
        error = null;

        try
        {
            var scope = new ManagementScope($@"\\.\{namespacePath}");
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
            using var results = searcher.Get();

            foreach (ManagementBaseObject item in results)
            {
                if (item is ManagementObject managementObject)
                {
                    rows.Add(managementObject);
                }
                else
                {
                    item.Dispose();
                }

                if (rows.Count >= MaxRows)
                {
                    error = TruncatedError;
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }

        return rows;
    }

    public static object? Value(ManagementBaseObject instance, string property)
    {
        try
        {
            return instance.Properties[property]?.Value;
        }
        catch
        {
            return null;
        }
    }

    public static string? Text(ManagementBaseObject instance, string property)
    {
        var value = Value(instance, property);
        var text = value is null ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    public static ulong? ULong(ManagementBaseObject instance, string property)
    {
        var value = Value(instance, property);
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToUInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public static uint? UInt(ManagementBaseObject instance, string property)
    {
        var value = Value(instance, property);
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToUInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }
}
