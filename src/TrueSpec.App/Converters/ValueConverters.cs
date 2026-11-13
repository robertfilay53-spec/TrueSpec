using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Avalonia.Media;
using TrueSpec.Core.Localization;
using TrueSpec.Core.Models;

namespace TrueSpec.App.Converters;

/// <summary>Maps a health band to the one accent color allowed for it.</summary>
public sealed class HealthStatusToBrushConverter : IValueConverter
{
    public static readonly HealthStatusToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is HealthStatus health ? health : HealthStatus.NotMeasurable;
        return status switch
        {
            HealthStatus.Healthy => new SolidColorBrush(Color.Parse("#22C55E")),
            HealthStatus.Watch => new SolidColorBrush(Color.Parse("#F59E0B")),
            HealthStatus.Problem => new SolidColorBrush(Color.Parse("#EF4444")),
            _ => new SolidColorBrush(Color.Parse("#6B7280")),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps a buyer check state to the same health colors.</summary>
public sealed class BuyerCheckStateToBrushConverter : IValueConverter
{
    public static readonly BuyerCheckStateToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is BuyerCheckState state ? state : BuyerCheckState.NotApplicable;
        return status switch
        {
            BuyerCheckState.Pass => new SolidColorBrush(Color.Parse("#22C55E")),
            BuyerCheckState.Warning => new SolidColorBrush(Color.Parse("#F59E0B")),
            BuyerCheckState.Fail => new SolidColorBrush(Color.Parse("#EF4444")),
            _ => new SolidColorBrush(Color.Parse("#6B7280")),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps a health band to a faint tinted background for chips.</summary>
public sealed class HealthStatusToTintConverter : IValueConverter
{
    public static readonly HealthStatusToTintConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is HealthStatus health ? health : HealthStatus.NotMeasurable;
        return status switch
        {
            HealthStatus.Healthy => new SolidColorBrush(Color.Parse("#1F22C55E")),
            HealthStatus.Watch => new SolidColorBrush(Color.Parse("#1FF59E0B")),
            HealthStatus.Problem => new SolidColorBrush(Color.Parse("#1FEF4444")),
            _ => new SolidColorBrush(Color.Parse("#1F6B7280")),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Parses SVG style path markup into an Avalonia geometry for nav icons.</summary>
public sealed class StringToGeometryConverter : IValueConverter
{
    public static readonly StringToGeometryConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string data && !string.IsNullOrWhiteSpace(data))
        {
            return Geometry.Parse(data);
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Formats a nullable percentage for display.</summary>
public sealed class HealthPercentToTextConverter : IValueConverter
{
    public static readonly HealthPercentToTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double percent ? $"{percent:0}%" : "Not measurable";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Shows a language code as its own display name ("en" as "English",
/// "de" as "Deutsch"). A code with no pack shows the code itself, never
/// a blank, so a future pack never renders as an empty picker row.
/// </summary>
public sealed class LanguageCodeToNameConverter : IValueConverter
{
    public static readonly LanguageCodeToNameConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string code || string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var normalized = LanguagePacks.Normalize(code);
        var pack = LanguagePacks.All.FirstOrDefault(
            p => string.Equals(p.Code, normalized, StringComparison.OrdinalIgnoreCase));
        return pack?.DisplayName ?? code;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
