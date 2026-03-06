// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

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

        // Handle number formatting with format specifier
        if (!string.IsNullOrWhiteSpace(format) && IsNumeric(value) && TryFormatNumber(value!, culture, format!, out string? numberResult))
        {
            return numberResult!;
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

    private static bool IsNumeric(object? value)
    {
        return value is decimal or double or float or int or long;
    }

    private static bool TryFormatNumber(object value, CultureInfo culture, string format, out string? result)
    {
        result = null;

        try
        {
            if (string.Equals(format, "currency", StringComparison.OrdinalIgnoreCase))
            {
                result = Convert.ToDecimal(value, culture).ToString("C", culture);
                return true;
            }

            if (format.StartsWith("number:", StringComparison.OrdinalIgnoreCase) && format.Length > 7)
            {
                string numberFormat = format.Substring(7);
                if (value is IFormattable formattable)
                {
                    result = formattable.ToString(numberFormat, culture);
                    return true;
                }
            }
        }
        catch (FormatException)
        {
            // Invalid format string — fall through to default conversion
        }

        return false;
    }
}
