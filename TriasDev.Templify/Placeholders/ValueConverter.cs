// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Formatting;
using TriasDev.Templify.Utilities;

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

        // Handle string formatting with format specifier
        if (value is string strValue && !string.IsNullOrWhiteSpace(format))
        {
            if (string.Equals(format, "uppercase", StringComparison.OrdinalIgnoreCase))
            {
                return SanitizeXml(strValue.ToUpper(culture));
            }

            if (string.Equals(format, "lowercase", StringComparison.OrdinalIgnoreCase))
            {
                return SanitizeXml(strValue.ToLower(culture));
            }
        }

        // Handle number formatting with format specifier
        if (!string.IsNullOrWhiteSpace(format) && IsNumeric(value) && TryFormatNumber(value!, culture, format!, out string? numberResult))
        {
            return numberResult!;
        }

        // Handle date formatting with format specifier
        if (!string.IsNullOrWhiteSpace(format) && TryFormatDate(value, culture, format!, out string? dateResult))
        {
            return dateResult!;
        }

        // Default conversion without format
        return SanitizeXml(value switch
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
        });
    }

    /// <summary>
    /// Removes characters that are invalid in XML 1.0 (e.g., 0x02 STX)
    /// to prevent OpenXML serialization failures.
    /// </summary>
    private static string SanitizeXml(string value)
    {
        return XmlCharacterSanitizer.Sanitize(value)!;
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
                if (value is IFormattable currencyFormattable)
                {
                    result = currencyFormattable.ToString("C", culture);
                    return true;
                }
            }

            if (format.StartsWith("number:", StringComparison.OrdinalIgnoreCase) && format.Length > 7)
            {
                string numberFormat = format[7..];
                if (value is IFormattable formattable)
                {
                    result = formattable.ToString(numberFormat, culture);
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            // Invalid format string or numeric overflow — fall through to default conversion
        }

        return false;
    }

    private static bool TryFormatDate(object? value, CultureInfo culture, string format, out string? result)
    {
        result = null;

        if (!format.StartsWith("date:", StringComparison.OrdinalIgnoreCase) || format.Length <= 5)
        {
            return false;
        }

        string dateFormat = format[5..];

        try
        {
            // Use DateTimeOffset to preserve timezone information when available
            DateTimeOffset? dateTimeOffset = value switch
            {
                DateTimeOffset dto => dto,
                DateTime dt => new DateTimeOffset(dt),
                string s when TryParseDateTime(s, culture, out DateTimeOffset parsed) => parsed,
                _ => null
            };

            if (dateTimeOffset.HasValue)
            {
                result = dateTimeOffset.Value.ToString(dateFormat, culture);
                return true;
            }
        }
        catch (FormatException)
        {
            // Invalid format string — fall through to default conversion
        }

        return false;
    }

    /// <summary>
    /// Tries to parse a date string, first with InvariantCulture (for ISO formats),
    /// then with the specified culture as a fallback.
    /// </summary>
    private static bool TryParseDateTime(string s, CultureInfo culture, out DateTimeOffset parsed)
    {
        // Try InvariantCulture first for reliable ISO date parsing (e.g., "2024-01-15", "2024-01-15T10:30:00+02:00")
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return true;
        }

        // Fall back to the specified culture for locale-specific formats (e.g., "15.01.2024" with de-DE)
        return DateTimeOffset.TryParse(s, culture, DateTimeStyles.None, out parsed);
    }
}
