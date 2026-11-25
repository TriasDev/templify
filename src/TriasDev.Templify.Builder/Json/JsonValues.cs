// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.RegularExpressions;
using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder.Json;

/// <summary>
/// How values are written in JSON definitions: lengths with units, colours as <c>#RRGGBB</c>, page sizes by name,
/// borders and boxes CSS-style. Always invariant, so a file reads the same under every culture.
/// </summary>
internal static partial class JsonValues
{
    private const double MillimetersPerPoint = 25.4 / 72.0;

    private static readonly (string Name, PageSize Size)[] _pageSizes =
    {
        ("A4", PageSize.A4), ("A5", PageSize.A5), ("Letter", PageSize.Letter), ("Legal", PageSize.Legal),
    };

    /// <summary>
    /// A length in its shortest exact form: whole or half points as points (<c>10pt</c>, <c>10.5pt</c>), else
    /// millimetres when a value with at most two decimals gives the same length (<c>25mm</c>), else points.
    /// </summary>
    public static string Length(Length value)
    {
        if (value.Millipoints % 500 == 0)
        {
            return Number(value.Millipoints / 1000.0) + "pt";
        }

        double millimeters = Math.Round(value.Millipoints / 1000.0 * MillimetersPerPoint, 2, MidpointRounding.AwayFromZero);
        if (Builder.Length.FromMillimeters(millimeters).Millipoints == value.Millipoints)
        {
            return Number(millimeters) + "mm";
        }

        return Number(value.Millipoints / 1000.0) + "pt";
    }

    /// <summary>Reads a length with a unit: <c>pt</c>, <c>mm</c>, <c>cm</c> or <c>in</c>.</summary>
    public static bool TryLength(string? text, out Length value)
    {
        value = default;
        Match match = text is null ? Match.Empty : LengthPattern().Match(text);
        if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double number))
        {
            return false;
        }

        try
        {
            value = match.Groups[2].Value switch
            {
                "pt" => Builder.Length.FromPoints(number),
                "mm" => Builder.Length.FromMillimeters(number),
                "cm" => Builder.Length.FromMillimeters(number * 10),
                _ => Builder.Length.FromPoints(number * 72),
            };
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>A colour as <c>#RRGGBB</c>, upper case.</summary>
    public static string Color(Color value) => "#" + value.Hex;

    /// <summary>Reads <c>#RRGGBB</c> in either case.</summary>
    public static bool TryColor(string? text, out Color value)
    {
        value = default;
        if (text is null || !ColorPattern().IsMatch(text))
        {
            return false;
        }

        value = Builder.Color.FromHex(text[1..]);
        return true;
    }

    /// <summary>The name of a named page size, or null.</summary>
    public static string? PageSizeName(PageSize size) => _pageSizes.FirstOrDefault(p => p.Size == size).Name;

    /// <summary>Reads a page size name.</summary>
    public static bool TryPageSize(string? name, out PageSize? size)
    {
        size = _pageSizes.FirstOrDefault(p => p.Name == name).Size;
        return size is not null;
    }

    /// <summary>A border line: <c>0.5pt #D4D4D4</c>.</summary>
    public static string Border(BorderLine line) => Length(line.Width) + " " + Color(line.Color);

    /// <summary>Reads a border line.</summary>
    public static bool TryBorder(string? text, out BorderLine? line)
    {
        line = null;
        string[] parts = text?.Split(' ') ?? [];
        if (parts.Length != 2 || !TryLength(parts[0], out Length width) || !TryColor(parts[1], out Color color))
        {
            return false;
        }

        line = new BorderLine(width, color);
        return true;
    }

    /// <summary>Four sides CSS-style: one value when all are equal, two for vertical/horizontal pairs, else four.</summary>
    public static string Box(Length top, Length right, Length bottom, Length left)
    {
        if (top == right && top == bottom && top == left)
        {
            return Length(top);
        }

        return top == bottom && right == left
            ? Length(top) + " " + Length(right)
            : string.Join(" ", Length(top), Length(right), Length(bottom), Length(left));
    }

    /// <summary>Reads one, two or four lengths.</summary>
    public static bool TryBox(string? text, out (Length Top, Length Right, Length Bottom, Length Left) box)
    {
        box = default;
        string[] parts = text?.Split(' ') ?? [];
        Length[] values = new Length[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!TryLength(parts[i], out values[i]))
            {
                return false;
            }
        }

        switch (values.Length)
        {
            case 1:
                box = (values[0], values[0], values[0], values[0]);
                return true;
            case 2:
                box = (values[0], values[1], values[0], values[1]);
                return true;
            case 4:
                box = (values[0], values[1], values[2], values[3]);
                return true;
            default:
                return false;
        }
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    [GeneratedRegex("^(-?[0-9]+(?:\\.[0-9]+)?)(pt|mm|cm|in)$", RegexOptions.CultureInvariant)]
    private static partial Regex LengthPattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
}
