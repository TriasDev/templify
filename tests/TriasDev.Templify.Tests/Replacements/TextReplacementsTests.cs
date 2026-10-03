// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Replacements;

namespace TriasDev.Templify.Tests.Replacements;

public class TextReplacementsTests
{
    #region HtmlEntities Preset Tests

    [Fact]
    public void HtmlEntities_ContainsExpectedReplacements()
    {
        // Act
        var entities = TextReplacements.HtmlEntities;

        // Assert
        Assert.Contains("<br>", entities.Keys);
        Assert.Contains("<br/>", entities.Keys);
        Assert.Contains("<br />", entities.Keys);
        Assert.Contains("&nbsp;", entities.Keys);
        Assert.Contains("&lt;", entities.Keys);
        Assert.Contains("&gt;", entities.Keys);
        Assert.Contains("&amp;", entities.Keys);
        Assert.Contains("&quot;", entities.Keys);
        Assert.Contains("&apos;", entities.Keys);
        Assert.Contains("&mdash;", entities.Keys);
        Assert.Contains("&ndash;", entities.Keys);
    }

    [Theory]
    [InlineData("<br>", "\n")]
    [InlineData("<br/>", "\n")]
    [InlineData("<br />", "\n")]
    [InlineData("<BR>", "\n")]
    [InlineData("<BR/>", "\n")]
    [InlineData("<BR />", "\n")]
    public void HtmlEntities_LineBreakVariations_MapToNewline(string input, string expected)
    {
        // Act
        var entities = TextReplacements.HtmlEntities;

        // Assert
        Assert.Equal(expected, entities[input]);
    }

    [Fact]
    public void HtmlEntities_Nbsp_MapsToNonBreakingSpace()
    {
        // Act
        var entities = TextReplacements.HtmlEntities;

        // Assert
        Assert.Equal("\u00A0", entities["&nbsp;"]);
    }

    [Theory]
    [InlineData("&lt;", "<")]
    [InlineData("&gt;", ">")]
    [InlineData("&amp;", "&")]
    [InlineData("&quot;", "\"")]
    [InlineData("&apos;", "'")]
    [InlineData("&mdash;", "\u2014")]
    [InlineData("&ndash;", "\u2013")]
    public void HtmlEntities_CommonEntities_MapCorrectly(string input, string expected)
    {
        // Act
        var entities = TextReplacements.HtmlEntities;

        // Assert
        Assert.Equal(expected, entities[input]);
    }

    #endregion

    #region Apply Method Tests

    [Fact]
    public void Apply_NullInput_ReturnsNull()
    {
        // Arrange
        string? input = null;
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input!, replacements);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Apply_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        string input = "";
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void Apply_NullReplacements_ReturnsInputUnchanged()
    {
        // Arrange
        string input = "Hello<br>World";
        Dictionary<string, string>? replacements = null;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("Hello<br>World", result);
    }

    [Fact]
    public void Apply_EmptyReplacements_ReturnsInputUnchanged()
    {
        // Arrange
        string input = "Hello<br>World";
        var replacements = new Dictionary<string, string>();

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("Hello<br>World", result);
    }

    [Fact]
    public void Apply_NoMatchingPatterns_ReturnsInputUnchanged()
    {
        // Arrange
        string input = "Hello World";
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Apply_SingleLineBreak_ReplacesCorrectly()
    {
        // Arrange
        string input = "Hello<br>World";
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("Hello\nWorld", result);
    }

    [Fact]
    public void Apply_MultipleLineBreaks_ReplacesAll()
    {
        // Arrange
        string input = "Line1<br>Line2<br/>Line3<br />Line4";
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("Line1\nLine2\nLine3\nLine4", result);
    }

    [Fact]
    public void Apply_HtmlEntities_ReplacesAllCorrectly()
    {
        // Arrange
        string input = "5 &lt; 10 &amp; 10 &gt; 5";
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("5 < 10 & 10 > 5", result);
    }

    [Fact]
    public void Apply_MixedContent_ReplacesAllPatterns()
    {
        // Arrange
        string input = "Hello&nbsp;World<br>&lt;test&gt;";
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("Hello\u00A0World\n<test>", result);
    }

    [Fact]
    public void Apply_QuotesAndApostrophes_ReplacesCorrectly()
    {
        // Arrange
        string input = "He said &quot;Hello&quot; and &apos;Goodbye&apos;";
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("He said \"Hello\" and 'Goodbye'", result);
    }

    [Fact]
    public void Apply_Dashes_ReplacesCorrectly()
    {
        // Arrange
        string input = "Contact us&nbsp;&mdash;&nbsp;we're here to help!";
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("Contact us\u00A0\u2014\u00A0we're here to help!", result);
    }

    [Fact]
    public void Apply_CaseSensitiveLineBreaks_UppercaseReplaced()
    {
        // Arrange - HtmlEntities includes uppercase variants
        string input = "Hello<BR>World<BR/>Test<BR />End";
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("Hello\nWorld\nTest\nEnd", result);
    }

    [Fact]
    public void Apply_CustomReplacements_WorksCorrectly()
    {
        // Arrange
        string input = "Hello COMPANY_NAME, welcome to PRODUCT!";
        var replacements = new Dictionary<string, string>
        {
            ["COMPANY_NAME"] = "Acme Corp",
            ["PRODUCT"] = "Templify"
        };

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("Hello Acme Corp, welcome to Templify!", result);
    }

    [Fact]
    public void Apply_CombinedPresetAndCustom_WorksCorrectly()
    {
        // Arrange
        string input = "Hello<br>COMPANY_NAME";
        var replacements = new Dictionary<string, string>(TextReplacements.HtmlEntities)
        {
            ["COMPANY_NAME"] = "Acme Corp"
        };

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("Hello\nAcme Corp", result);
    }

    [Fact]
    public void Apply_ChainedReplacements_RequiresExplicitPattern()
    {
        // Arrange - Test that replacements are done in a single pass
        // The input "&amp;nbsp;" does NOT contain literal "&nbsp;" substring,
        // so &nbsp; replacement won't match until &amp; is replaced first.
        // Since we do a single pass, the result depends on enumeration order.
        string input = "&amp;nbsp;";
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert - With single-pass replacement:
        // - "&amp;" matches and gets replaced with "&", resulting in "&nbsp;"
        // - The iteration continues but &nbsp; was already checked, so no further replacement
        // This is expected behavior - use explicit patterns for chained replacements
        Assert.Equal("&nbsp;", result);
    }

    [Fact]
    public void Apply_ConsecutiveEntities_ReplacesAll()
    {
        // Arrange
        string input = "&lt;&gt;&amp;";
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("<>&", result);
    }

    [Fact]
    public void Apply_OnlyEntity_ReplacesCorrectly()
    {
        // Arrange
        string input = "&nbsp;";
        var replacements = TextReplacements.HtmlEntities;

        // Act
        string? result = TextReplacements.Apply(input, replacements);

        // Assert
        Assert.Equal("\u00A0", result);
    }

    #endregion
}
