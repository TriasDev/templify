// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace TriasDev.Templify.Builder;

/// <summary>
/// A length in a document: a font size, a spacing, an indent. Stored as an integer number of 1/1000 points, so
/// lengths add up exactly and every writer rounds only once, to its own unit.
/// </summary>
/// <remarks>Write lengths with the extension methods in <see cref="Units"/>: <c>12.Pt()</c>, <c>25.Mm()</c>.</remarks>
public readonly struct Length : IEquatable<Length>, IComparable<Length>
{
    private const double MillimetersPerPoint = 25.4 / 72.0;

    private Length(int millipoints)
    {
        Millipoints = millipoints;
    }

    /// <summary>Gets the zero length.</summary>
    public static Length Zero => default;

    /// <summary>Gets the length in points.</summary>
    public double Points => Millipoints / 1000.0;

    /// <summary>Gets the length in millimeters.</summary>
    public double Millimeters => Points * MillimetersPerPoint;

    /// <summary>Gets the length in 1/1000 points.</summary>
    internal int Millipoints { get; }

    /// <summary>Creates a length from points (1/72 inch).</summary>
    /// <param name="points">The length in points.</param>
    /// <returns>The length, rounded to 1/1000 point.</returns>
    public static Length FromPoints(double points) => new Length(ToMillipoints(points * 1000.0, nameof(points)));

    /// <summary>Creates a length from millimeters.</summary>
    /// <param name="millimeters">The length in millimeters.</param>
    /// <returns>The length, rounded to 1/1000 point.</returns>
    public static Length FromMillimeters(double millimeters) =>
        new Length(ToMillipoints(millimeters / MillimetersPerPoint * 1000.0, nameof(millimeters)));

    /// <summary>Adds two lengths.</summary>
    /// <param name="left">The first length.</param>
    /// <param name="right">The second length.</param>
    /// <returns>The sum.</returns>
    public static Length operator +(Length left, Length right) => new Length(checked(left.Millipoints + right.Millipoints));

    /// <summary>Subtracts a length.</summary>
    /// <param name="left">The length to subtract from.</param>
    /// <param name="right">The length to subtract.</param>
    /// <returns>The difference.</returns>
    public static Length operator -(Length left, Length right) => new Length(checked(left.Millipoints - right.Millipoints));

    /// <summary>Negates a length, for example for a hanging indent: <c>-4.5.Mm()</c>.</summary>
    /// <param name="value">The length.</param>
    /// <returns>The negated length.</returns>
    public static Length operator -(Length value) => new Length(checked(-value.Millipoints));

    /// <summary>Compares two lengths.</summary>
    /// <param name="left">The first length.</param>
    /// <param name="right">The second length.</param>
    /// <returns><see langword="true"/> when both are equal.</returns>
    public static bool operator ==(Length left, Length right) => left.Equals(right);

    /// <summary>Compares two lengths.</summary>
    /// <param name="left">The first length.</param>
    /// <param name="right">The second length.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(Length left, Length right) => !left.Equals(right);

    /// <summary>Compares two lengths.</summary>
    /// <param name="left">The first length.</param>
    /// <param name="right">The second length.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is shorter.</returns>
    public static bool operator <(Length left, Length right) => left.Millipoints < right.Millipoints;

    /// <summary>Compares two lengths.</summary>
    /// <param name="left">The first length.</param>
    /// <param name="right">The second length.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is longer.</returns>
    public static bool operator >(Length left, Length right) => left.Millipoints > right.Millipoints;

    /// <summary>Compares two lengths.</summary>
    /// <param name="left">The first length.</param>
    /// <param name="right">The second length.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not longer.</returns>
    public static bool operator <=(Length left, Length right) => left.Millipoints <= right.Millipoints;

    /// <summary>Compares two lengths.</summary>
    /// <param name="left">The first length.</param>
    /// <param name="right">The second length.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not shorter.</returns>
    public static bool operator >=(Length left, Length right) => left.Millipoints >= right.Millipoints;

    /// <inheritdoc />
    public int CompareTo(Length other) => Millipoints.CompareTo(other.Millipoints);

    /// <inheritdoc />
    public bool Equals(Length other) => Millipoints == other.Millipoints;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Length other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Millipoints;

    /// <summary>Returns the length in points, for example <c>10.5pt</c>.</summary>
    /// <returns>The length in points, invariant culture.</returns>
    public override string ToString() => FormatPoints() + "pt";

    /// <summary>The shorter of two lengths.</summary>
    internal static Length Min(Length a, Length b) => a <= b ? a : b;

    /// <summary>The length in points with up to three decimals, invariant culture, without a unit.</summary>
    internal string FormatPoints() => (Millipoints / 1000m).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>The length in twentieths of a point (twips), as WordprocessingML spacing and indents use.</summary>
    internal int Twips => (int)Math.Round(Millipoints / 50.0, MidpointRounding.AwayFromZero);

    /// <summary>The length in half points, as WordprocessingML font sizes use.</summary>
    internal int HalfPoints => (int)Math.Round(Millipoints / 500.0, MidpointRounding.AwayFromZero);

    private static int ToMillipoints(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The length is not a finite value in range.");
        }

        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
