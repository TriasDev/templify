using System.Globalization;
using TriasDev.Templify.Formatting;

namespace TriasDev.Templify.Placeholders;

/// <summary>
/// Converts object values to string representations for document replacement.
/// </summary>
internal static class ValueConverter
{
    /// <summary>
    /// Converts an object value to its string representation using the specified culture.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="culture">The culture to use for formatting culture-sensitive values.</param>
    /// <returns>The string representation of the value.</returns>
    public static string ConvertToString(object? value, CultureInfo culture)
    {
        return ConvertToString(value, culture, null, null);
    }

    /// <summary>
    /// Converts an object value to its string representation using the specified culture and optional format.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="culture">The culture to use for formatting culture-sensitive values.</param>
    /// <param name="format">Optional format specifier (e.g., "checkbox", "yesno").</param>
    /// <param name="formatterRegistry">Optional boolean formatter registry for custom formats.</param>
    /// <returns>The string representation of the value.</returns>
    public static string ConvertToString(object? value, CultureInfo culture, string? format, BooleanFormatterRegistry? formatterRegistry)
    {
        // Handle boolean formatting with format specifier
        if (value is bool boolValue && !string.IsNullOrWhiteSpace(format))
        {
            var registry = formatterRegistry ?? new BooleanFormatterRegistry(culture);
            if (registry.TryFormat(boolValue, format, out string? formattedValue))
            {
                return formattedValue!;
            }
            // Fall through to default formatting if format not found
        }

        // Default conversion without format
        return value switch
        {
            null => string.Empty,
            string str => str,
            DateTime dateTime => dateTime.ToString(culture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString(culture),
            decimal dec => dec.ToString(culture),
            double dbl => dbl.ToString(culture),
            float flt => flt.ToString(culture),
            int integer => integer.ToString(culture),
            long lng => lng.ToString(culture),
            bool boolean => boolean.ToString(),
            _ => value.ToString() ?? string.Empty
        };
    }
}
