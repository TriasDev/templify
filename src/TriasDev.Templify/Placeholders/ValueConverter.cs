// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
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
    /// Default boolean formatter registries, one per language (the built-in formatters depend only on
    /// <see cref="CultureInfo.TwoLetterISOLanguageName"/>). They are never exposed, so nothing can
    /// register into them.
    /// </summary>
    private static readonly ConcurrentDictionary<string, BooleanFormatterRegistry> _defaultBooleanFormatterRegistries =
        new(StringComparer.Ordinal);

    private static BooleanFormatterRegistry GetDefaultBooleanFormatterRegistry(CultureInfo culture) =>
        _defaultBooleanFormatterRegistries.GetOrAdd(
            culture.TwoLetterISOLanguageName,
            static (_, c) => new BooleanFormatterRegistry(c),
            culture);

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
            BooleanFormatterRegistry registry = formatterRegistry ?? GetDefaultBooleanFormatterRegistry(culture);
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
                return strValue.ToUpper(culture);
            }

            if (string.Equals(format, "lowercase", StringComparison.OrdinalIgnoreCase))
            {
                return strValue.ToLower(culture);
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
        return value switch
        {
            null => string.Empty,
            string str => str,
            DateTime dateTime => FormatDateSafe(dateTime, null, culture),
            DateTimeOffset dateTimeOffset => FormatDateSafe(dateTimeOffset, null, culture),
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
        return NumericValue.IsNumericType(value);
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
                DateTime dt => ToDateTimeOffsetSafe(dt),
                string s when TryParseDateTime(s, culture, out DateTimeOffset parsed) => parsed,
                _ => null
            };

            if (dateTimeOffset.HasValue)
            {
                result = FormatDateSafe(dateTimeOffset.Value, dateFormat, culture);
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
    /// Converts a <see cref="DateTime"/> to a <see cref="DateTimeOffset"/> without throwing.
    /// <c>new DateTimeOffset(dt)</c> applies the local UTC offset and throws for values near
    /// <see cref="DateTime.MinValue"/>/<see cref="DateTime.MaxValue"/> when the machine offset is
    /// positive/negative (e.g. an unset <c>DateTime</c> in Europe/Berlin). In that case the
    /// wall-clock value is kept and a zero offset is used.
    /// </summary>
    private static DateTimeOffset ToDateTimeOffsetSafe(DateTime dt)
    {
        try
        {
            return new DateTimeOffset(dt);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Formats a date with the given culture, falling back to <see cref="CultureInfo.InvariantCulture"/>
    /// when the value lies outside the range of the culture's calendar (e.g. <see cref="DateTime.MinValue"/>
    /// with the Um Al-Qura calendar of ar-SA), so a single placeholder never fails the whole document.
    /// </summary>
    private static string FormatDateSafe(IFormattable date, string? format, CultureInfo culture)
    {
        try
        {
            return date.ToString(format, culture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return date.ToString(format, CultureInfo.InvariantCulture);
        }
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
