// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Json;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Reading conditions back from their Templify text (#274).</summary>
public sealed class ConditionParserTests
{
    private static TemplatePath P(string path) => TemplatePath.Root(path);

    private static Condition Parse(string text) => ConditionParser.Parse(text, TemplatePath.Root);

    public static TheoryData<Condition> Conditions => new TheoryData<Condition>
    {
        (Condition)P("order.isPaid"),
        P("order.status").Eq("paid"),
        P("order.status").NotEq("open"),
        P("order.total").Gt(1000),
        P("order.total").Ge(0.5),
        P("order.total").Lt(-2.5),
        P("order.total").Le(3),
        P("order.total").NotEq(0),
        P("role").In("Admin", "Owner"),
        P("name").Contains("a\"b\\c"),
        P("name").StartsWith("Dr. "),
        P("name").EndsWith("GmbH"),
        P("notes").Exists,
        P("notes").IsEmpty,
        P("notes").IsNotEmpty,
        !P("order.isPaid"),
        !P("order.total").Gt(1),
        P("a") & P("b") | P("c"),
        P("a") & (P("b") | P("c")),
        P("a") & P("b") & P("c"),
        P("a") & (P("b") & P("c")),
        !(P("a") | P("b")),
        (Condition)P("exists"),
        P("in").Eq("x"),
        P("lines.amount").Gt(-2.5),
    };

    [Theory]
    [MemberData(nameof(Conditions))]
    public void Parse_ReadsWhatTheEmitterWrites(Condition condition)
    {
        string text = condition.ToString();

        Condition parsed = Parse(text);

        Assert.Equal(text, parsed.ToString());
    }

    [Fact]
    public void Parse_ResolvesPathsThroughTheResolver()
    {
        List<string> seen = new List<string>();

        Condition parsed = ConditionParser.Parse("line.amount > 0 and [exists]", p =>
        {
            seen.Add(p);
            return TemplatePath.Root(p);
        });

        Assert.Equal(new[] { "line.amount", "exists" }, seen);
        Assert.Equal("line.amount > 0 and [exists]", parsed.ToString());
    }

    [Fact]
    public void Parse_AcceptsExtraSpacesAndParentheses()
    {
        Assert.Equal("a and b", Parse("  ( a )   and (b)").ToString());
    }

    [Theory]
    [InlineData("a >", 3)]
    [InlineData("(a", 2)]
    [InlineData("a and", 5)]
    [InlineData("", 0)]
    [InlineData("a b", 2)]
    [InlineData("a in (\"x\"", 9)]
    public void Parse_Malformed_ReportsTheColumn(string text, int column)
    {
        FormatException exception = Assert.Throws<FormatException>(() => Parse(text));

        Assert.Contains($"column {column}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_BareWordLiteral_AsksForQuotes()
    {
        FormatException exception = Assert.Throws<FormatException>(() => Parse("status = paid"));

        Assert.Contains("quote", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_UnterminatedString()
    {
        Assert.Throws<FormatException>(() => Parse("a = \"x"));
    }
}
