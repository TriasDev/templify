using System.Globalization;

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
