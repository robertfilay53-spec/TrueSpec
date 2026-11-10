namespace TrueSpec.Collectors.Windows.Wmi;

using System.Globalization;
using System.Management;

/// <summary>One CIM instance flattened into a dictionary, with safe getters.</summary>
internal sealed class WmiRow
{
    private readonly Dictionary<string, object?> _values;

    public WmiRow(Dictionary<string, object?> values)
    {
        _values = values;
    }

    public object? this[string name] =>
        _values.TryGetValue(name, out var value) ? value : null;

    public string? String(string name)
    {
        var value = this[name];
        if (value is null)
        {
            return null;
        }

        if (value is string[] array)
        {
            var text = string.Join(", ", array.Where(v => !string.IsNullOrWhiteSpace(v)));
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        var result = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
    }

    public string? Solid(string name)
    {
        var value = String(name);
        return value?.Replace("\0", string.Empty, StringComparison.Ordinal).Trim();
    }

    public ulong? UInt64(string name)
    {
        var value = this[name];
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public uint? UInt32(string name)
    {
        var value = this[name];
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public ushort? UInt16(string name)
    {
        var value = this[name];
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToUInt16(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public int? Int32(string name)
    {
        var value = this[name];
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public bool? Bool(string name)
    {
        var value = this[name];
        if (value is null)
        {
            return null;
        }

        if (value is bool flag)
        {
            return flag;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return bool.TryParse(text, out var parsed) ? parsed : null;
    }

    public string? DateText(string name)
    {
        var date = Date(name);
        return date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public DateTimeOffset? Date(string name)
    {
        var value = String(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var dateTime = ManagementDateTimeConverter.ToDateTime(value);
            return new DateTimeOffset(dateTime);
        }
        catch
        {
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed)
                ? parsed
                : null;
        }
    }

    public string[]? StringArray(string name)
    {
        var value = this[name];
        if (value is null)
        {
            return null;
        }

        if (value is string[] strings)
        {
            return strings;
        }

        if (value is Array array)
        {
            return array.Cast<object?>()
                .Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .ToArray();
        }

        return null;
    }

    public uint[]? UInt32Array(string name)
    {
        var value = this[name];
        if (value is uint[] uints)
        {
            return uints;
        }

        if (value is Array array)
        {
            try
            {
                return array.Cast<object?>()
                    .Select(v => Convert.ToUInt32(v, CultureInfo.InvariantCulture))
                    .ToArray();
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public ushort[]? UInt16Array(string name)
    {
        var value = this[name];
        if (value is ushort[] ushorts)
        {
            return ushorts;
        }

        if (value is Array array)
        {
            try
            {
                return array.Cast<object?>()
                    .Select(v => Convert.ToUInt16(v, CultureInfo.InvariantCulture))
                    .ToArray();
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
