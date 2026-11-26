// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Json;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Strings with placeholders in JSON definitions: <c>{{path}}</c>, <c>{{path:format}}</c>, <c>{{(condition):format}}</c>.</summary>
public sealed class TextTemplateTests
{
    public static TheoryData<string, string> Valid => new TheoryData<string, string>
    {
        {
            "Auftrag {{order.number}} · {{order.date:date:d. MMMM yyyy}}",
            "L(Auftrag )¦V(order.number,-)¦L( · )¦V(order.date,date:d. MMMM yyyy)"
        },
        { "{{a}}{{b}}", "V(a,-)¦V(b,-)" },
        { "plain", "L(plain)" },
        { "", "L()" },
        { "a }} b", "L(a }} b)" },
        { "a } { b", "L(a } { b)" },
        { "{{order.date:date:HH:mm}}", "V(order.date,date:HH:mm)" },
        { "{{line.tags:join:\" · \"}}", "V(line.tags,join:\" · \")" },
        { "x {{@number}}.", "L(x )¦V(@number,-)¦L(.)" },
        {
            "Free: {{(order.isExpress and order.total > 1000):yesno}}",
            "L(Free: )¦E(order.isExpress and order.total > 1000,yesno)"
        },
        { "{{(a = \"x):y\"):yesno}}", "E(a = \"x):y\",yesno)" },
        { "{{(a = 'it''s')}}", "E(a = 'it''s',-)" },
        { "{{(a = \"q\\\")\"):yesno}}", "E(a = \"q\\\")\",yesno)" },
        { "{{((a or b) and c)}}", "E((a or b) and c,-)" },
    };

    [Theory]
    [MemberData(nameof(Valid))]
    public void Parse_SplitsIntoPieces(string text, string expected)
    {
        Assert.Equal(expected, Describe(TextTemplate.Parse(text)));
    }

    [Theory]
    [MemberData(nameof(Valid))]
    public void Write_IsTheInverseOfParse(string text, string pieces)
    {
        _ = pieces;
        Assert.Equal(text, TextTemplate.Write(TextTemplate.Parse(text)));
    }

    private static string Describe(IEnumerable<TextPiece> pieces) => string.Join("¦", pieces.Select(p => p switch
    {
        TextPiece.Literal l => "L(" + l.Text + ")",
        TextPiece.Value v => "V(" + v.Path + "," + (v.Format ?? "-") + ")",
        TextPiece.Expression e => "E(" + e.Condition + "," + (e.Format ?? "-") + ")",
        _ => "?",
    }));

    [Theory]
    [InlineData("{{", "{{")]
    [InlineData("x {{a", "{{a")]
    [InlineData("{{}}", "{{}}")]
    [InlineData("{{:yesno}}", "{{:yesno}}")]
    [InlineData("{{(a}}", "{{(a}}")]
    [InlineData("{{(a) junk}}", "{{(a) junk}}")]
    [InlineData("{{(a):}}", "{{(a):}}")]
    [InlineData("{{a:}}", "{{a:}}")]
    [InlineData("{{()}}", "{{()}}")]
    public void Parse_RejectsMalformedPlaceholders(string text, string quoted)
    {
        FormatException exception = Assert.Throws<FormatException>(() => TextTemplate.Parse(text));
        Assert.Contains(quoted, exception.Message, StringComparison.Ordinal);
    }
}
