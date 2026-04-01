// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests;

public class XmlCharacterSanitizerTests
{
    [Fact]
    public void Sanitize_NullInput_ReturnsNull()
    {
        Assert.Null(XmlCharacterSanitizer.Sanitize(null!));
    }

    [Fact]
    public void Sanitize_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, XmlCharacterSanitizer.Sanitize(string.Empty));
    }

    [Fact]
    public void Sanitize_NormalText_ReturnsUnchanged()
    {
        string input = "Hello, World! This is normal text with numbers 123 and symbols @#$.";
        Assert.Equal(input, XmlCharacterSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_PreservesValidWhitespace()
    {
        string input = "Line1\tTabbed\nLine2\rLine3\r\nLine4";
        Assert.Equal(input, XmlCharacterSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_PreservesUnicodeText()
    {
        string input = "Hinweisgebersystem \u00fc\u00f6\u00e4\u00df \u00a7 14 Abs 1";
        Assert.Equal(input, XmlCharacterSanitizer.Sanitize(input));
    }

    [Theory]
    [InlineData('\x00')]
    [InlineData('\x01')]
    [InlineData('\x02')]
    [InlineData('\x03')]
    [InlineData('\x04')]
    [InlineData('\x05')]
    [InlineData('\x06')]
    [InlineData('\x07')]
    [InlineData('\x08')]
    [InlineData('\x0B')]
    [InlineData('\x0C')]
    [InlineData('\x0E')]
    [InlineData('\x0F')]
    [InlineData('\x10')]
    [InlineData('\x1F')]
    public void Sanitize_RemovesInvalidXmlCharacter(char invalidChar)
    {
        string input = $"before{invalidChar}after";
        Assert.Equal("beforeafter", XmlCharacterSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_RemovesMultipleInvalidCharacters()
    {
        string input = "text\u0002with\u0003multiple\u0004invalid\u0005chars";
        Assert.Equal("textwithmultipleinvalidchars", XmlCharacterSanitizer.Sanitize(input));
    }

    [Theory]
    [InlineData('\uFFFE')]
    [InlineData('\uFFFF')]
    public void Sanitize_RemovesNonCharacterCodePoints(char invalidChar)
    {
        string input = $"before{invalidChar}after";
        Assert.Equal("beforeafter", XmlCharacterSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_StringWithoutInvalidChars_ReturnsSameInstance()
    {
        string input = "No invalid characters here";
        string result = XmlCharacterSanitizer.Sanitize(input);
        Assert.Same(input, result);
    }
}
