// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace TriasDev.Templify.Builder;

/// <summary>An opaque RGB colour.</summary>
public readonly struct Color : IEquatable<Color>
{
    private Color(byte red, byte green, byte blue)
    {
        R = red;
        G = green;
        B = blue;
    }

    /// <summary>Gets the red component.</summary>
    public byte R { get; }

    /// <summary>Gets the green component.</summary>
    public byte G { get; }

    /// <summary>Gets the blue component.</summary>
    public byte B { get; }

    /// <summary>Creates a colour from its components.</summary>
    /// <param name="red">The red component.</param>
    /// <param name="green">The green component.</param>
    /// <param name="blue">The blue component.</param>
    /// <returns>The colour.</returns>
    public static Color FromRgb(byte red, byte green, byte blue) => new Color(red, green, blue);

    /// <summary>Creates a colour from six hex digits, with or without a leading <c>#</c>: <c>1F4E79</c>, <c>#1f4e79</c>.</summary>
    /// <param name="hex">The hex value.</param>
    /// <returns>The colour.</returns>
    /// <exception cref="ArgumentException">The value is not six hex digits.</exception>
    public static Color FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        ReadOnlySpan<char> digits = hex.AsSpan();
        if (digits.StartsWith("#"))
        {
            digits = digits[1..];
        }

        if (digits.Length != 6 || !int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int value))
        {
            throw new ArgumentException($"'{hex}' is not a colour: expected six hex digits, for example \"1F4E79\".", nameof(hex));
        }

        return new Color((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    /// <summary>Returns the colour as <c>#RRGGBB</c>.</summary>
    /// <returns>The hex value with a leading <c>#</c>.</returns>
    public override string ToString() => "#" + Hex;

    /// <summary>Six upper-case hex digits, as WordprocessingML writes colours.</summary>
    internal string Hex => string.Create(CultureInfo.InvariantCulture, $"{R:X2}{G:X2}{B:X2}");

    /// <inheritdoc />
    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Color other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => (R << 16) | (G << 8) | B;

    /// <summary>Compares two colours.</summary>
    /// <param name="left">The first colour.</param>
    /// <param name="right">The second colour.</param>
    /// <returns><see langword="true"/> when both are equal.</returns>
    public static bool operator ==(Color left, Color right) => left.Equals(right);

    /// <summary>Compares two colours.</summary>
    /// <param name="left">The first colour.</param>
    /// <param name="right">The second colour.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(Color left, Color right) => !left.Equals(right);
}
