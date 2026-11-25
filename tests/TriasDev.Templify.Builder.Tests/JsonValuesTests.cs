// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>How values are written in JSON definitions (#274).</summary>
public sealed class JsonValuesTests
{
    public static TheoryData<Length, string> Lengths => new TheoryData<Length, string>
    {
        { 10.Pt(), "10pt" },
        { 10.5.Pt(), "10.5pt" },
        { 0.Pt(), "0pt" },
        { (-6).Mm(), "-6mm" },
        { 25.Mm(), "25mm" },
        { 12.5.Mm(), "12.5mm" },
        { Length.FromPoints(10.123), "10.123pt" },
    };

    [Theory]
    [MemberData(nameof(Lengths))]
    public void Length_IsWrittenInTheShortestExactForm(Length value, string text)
    {
        Assert.Equal(text, JsonValues.Length(value));
        Assert.True(JsonValues.TryLength(text, out Length back));
        Assert.Equal(value, back);
    }

    [Theory]
    [InlineData("1in", 72.0)]
    [InlineData("1cm", 28.346)]
    [InlineData("2.5pt", 2.5)]
    public void Length_ReadsOtherUnits(string text, double points)
    {
        Assert.True(JsonValues.TryLength(text, out Length value));
        Assert.Equal(points, value.Points, 3);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("pt")]
    [InlineData("1,5pt")]
    [InlineData("10 pt")]
    [InlineData("")]
    public void Length_RejectsMalformedText(string text)
    {
        Assert.False(JsonValues.TryLength(text, out _));
    }

    [Fact]
    public void Color_IsUpperCaseHex_AndReadsEitherCase()
    {
        Assert.Equal("#1F4E79", JsonValues.Color(Color.FromHex("1f4e79")));
        Assert.True(JsonValues.TryColor("#abcdef", out Color color));
        Assert.Equal(Color.FromHex("ABCDEF"), color);
        Assert.False(JsonValues.TryColor("#12345", out _));
        Assert.False(JsonValues.TryColor("123456", out _));
    }

    [Fact]
    public void PageSize_ByName()
    {
        Assert.Equal("A4", JsonValues.PageSizeName(PageSize.A4));
        Assert.Equal("Letter", JsonValues.PageSizeName(PageSize.Letter));
        Assert.Null(JsonValues.PageSizeName(new PageSize(100.Mm(), 100.Mm())));
        Assert.True(JsonValues.TryPageSize("A5", out PageSize? size));
        Assert.Equal(PageSize.A5, size);
        Assert.False(JsonValues.TryPageSize("A3", out _));
    }

    [Fact]
    public void Border_IsWidthAndColour()
    {
        BorderLine line = new BorderLine(0.5.Pt(), Color.FromHex("D4D4D4"));

        Assert.Equal("0.5pt #D4D4D4", JsonValues.Border(line));
        Assert.True(JsonValues.TryBorder("0.5pt #d4d4d4", out BorderLine? back));
        Assert.Equal(line, back);
        Assert.False(JsonValues.TryBorder("0.5pt", out _));
    }

    [Theory]
    [InlineData(3, 3, 3, 3, "3pt")]
    [InlineData(3, 5, 3, 5, "3pt 5pt")]
    [InlineData(1, 2, 3, 4, "1pt 2pt 3pt 4pt")]
    public void Box_IsShortest(double top, double right, double bottom, double left, string text)
    {
        Assert.Equal(text, JsonValues.Box(top.Pt(), right.Pt(), bottom.Pt(), left.Pt()));
        Assert.True(JsonValues.TryBox(text, out (Length Top, Length Right, Length Bottom, Length Left) box));
        Assert.Equal((top.Pt(), right.Pt(), bottom.Pt(), left.Pt()), box);
        Assert.False(JsonValues.TryBox("1pt 2pt 3pt", out _));
    }

    [Fact]
    public void Values_DoNotDependOnTheCurrentCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "tr-TR", "ar-SA", "de-DE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Assert.Equal("10.5pt", JsonValues.Length(10.5.Pt()));
                Assert.True(JsonValues.TryLength("12.5mm", out Length value));
                Assert.Equal(12.5.Mm(), value);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
