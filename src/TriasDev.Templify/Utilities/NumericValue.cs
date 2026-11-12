// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;

namespace TriasDev.Templify.Utilities;

/// <summary>
/// Single place that decides what counts as a number and how numbers of different CLR types
/// (and JSON numbers) are compared, so that <c>10 = 10.00m</c>, <c>5L = 5</c> and JSON
/// <c>10.50</c> vs. literal <c>10.5</c> behave the same everywhere (see issue #142).
/// </summary>
/// <remarks>
/// Integer types and <see cref="decimal"/> are compared exactly as <see cref="decimal"/>.
/// <see cref="float"/>/<see cref="double"/> values take part in the <see cref="decimal"/>
/// comparison after rounding to the type's significant digits (7 for <see cref="float"/>,
/// 15 for <see cref="double"/>), so the literal <c>0.1</c> equals <c>0.1m</c>. Before .NET 11,
/// <see cref="Convert.ToDecimal(double)"/> does this rounding and is used unchanged. .NET 11 made
/// that conversion exact (<c>0.1</c> → <c>0.1000000000000000055…</c>), so on .NET 11 and later the
/// value is formatted with <c>G15</c>/<c>G7</c> and parsed back. Both agree for values with up to
/// 15 (double) / 7 (float) significant digits above 1e-13; beyond that they can differ in the last
/// digit, because the old conversion rounded through <see cref="double"/> arithmetic. Two values of the same
/// floating type, and values outside the <see cref="decimal"/> range (including infinities),
/// are compared as <see cref="double"/>. <see cref="double.NaN"/> is equal to nothing and is
/// not ordered against anything.
/// </remarks>
internal readonly struct NumericValue
{
    private enum NumberKind
    {
        Exact,
        Single,
        Double,
    }

    private readonly NumberKind _kind;
    private readonly decimal? _decimal;
    private readonly double _double;

    private NumericValue(NumberKind kind, decimal? dec, double dbl)
    {
        _kind = kind;
        _decimal = dec;
        _double = dbl;
    }

    /// <summary>True when the value is <see cref="double.NaN"/> (or <see cref="float.NaN"/>).</summary>
    public bool IsNaN => _decimal is null && double.IsNaN(_double);

    /// <summary>True when the value is (positive or negative) zero.</summary>
    public bool IsZero => _decimal is decimal d ? d == 0m : _double == 0d;

    /// <summary>True for every CLR numeric primitive (integers, <c>float</c>, <c>double</c>, <c>decimal</c>).</summary>
    public static bool IsNumericType(object? value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    /// <summary>
    /// Normalizes a CLR numeric primitive or a <see cref="JsonElement"/> number. Strings are NOT
    /// treated as numbers here; callers decide whether a string takes part numerically.
    /// </summary>
    public static bool TryFrom(object? value, out NumericValue number)
    {
        switch (value)
        {
            case decimal or int or long or short or byte or sbyte or ushort or uint or ulong:
                decimal exact = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                number = new NumericValue(NumberKind.Exact, exact, (double)exact);
                return true;
            case float f:
                number = FromFloating(NumberKind.Single, f);
                return true;
            case double d:
                number = FromFloating(NumberKind.Double, d);
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } json:
                if (json.TryGetDecimal(out decimal jd))
                {
                    number = new NumericValue(NumberKind.Exact, jd, (double)jd);
                    return true;
                }

                number = FromFloating(NumberKind.Double, json.GetDouble());
                return true;
            default:
                number = default;
                return false;
        }
    }

    /// <summary>Parses a numeric string with <see cref="CultureInfo.InvariantCulture"/>.</summary>
    public static bool TryParse(string? text, out NumericValue number)
    {
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal dec))
        {
            number = new NumericValue(NumberKind.Exact, dec, (double)dec);
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double dbl))
        {
            number = FromFloating(NumberKind.Double, dbl);
            return true;
        }

        number = default;
        return false;
    }

    /// <summary>Numeric equality across types. NaN is equal to nothing.</summary>
    public bool NumericEquals(NumericValue other) => TryCompareTo(other, out int cmp) && cmp == 0;

    /// <summary>Numeric ordering across types. Returns false when either side is NaN.</summary>
    public bool TryCompareTo(NumericValue other, out int cmp)
    {
        cmp = 0;
        if (IsNaN || other.IsNaN)
        {
            return false;
        }

        bool sameFloatingKind = _kind == other._kind && _kind != NumberKind.Exact;
        if (!sameFloatingKind && _decimal is decimal l && other._decimal is decimal r)
        {
            cmp = l.CompareTo(r);
            return true;
        }

        cmp = _double.CompareTo(other._double);
        return true;
    }

    private static NumericValue FromFloating(NumberKind kind, double value)
    {
        decimal? dec = null;
        if (!double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) < 7.9e28)
        {
            dec = _convertToDecimalIsExact ? RoundToDecimal(kind, value) : LegacyConvertToDecimal(kind, value);
        }

        return new NumericValue(kind, dec, value);
    }

    /// <summary>True on .NET 11 and later, where <see cref="Convert.ToDecimal(double)"/> no longer rounds.</summary>
    private static readonly bool _convertToDecimalIsExact = Environment.Version.Major >= 11;

    private static decimal? LegacyConvertToDecimal(NumberKind kind, double value)
    {
        try
        {
            return kind == NumberKind.Single ? Convert.ToDecimal((float)value) : Convert.ToDecimal(value);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static decimal? RoundToDecimal(NumberKind kind, double value)
    {
        string rounded = kind == NumberKind.Single
            ? ((float)value).ToString("G7", CultureInfo.InvariantCulture)
            : value.ToString("G15", CultureInfo.InvariantCulture);
        return decimal.TryParse(rounded, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : null;
    }
}
