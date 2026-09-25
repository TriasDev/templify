// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

/// <summary>
/// Tests for string escapes, unterminated strings and reserved-word handling in conditions (#149).
/// </summary>
public sealed class ConditionEscapeAndKeywordTests
{
    private static IReadOnlyList<ConditionToken> Lex(string expression) => new ConditionLexer().Tokenize(expression);

    #region Ported from audit/2026-09-confirming-tests

    [Fact]
    public void Cond_ReservedWord_VariableNamedEmpty()
    {
        // a variable called "Empty" or "Exists" or "In" can still be referenced where an operand is expected
        Assert.True(new ConditionEvaluator().Evaluate("Exists", new Dictionary<string, object> { ["Exists"] = true }));
    }

    [Fact]
    public void Cond_EscapedBackslashAtEndOfString()
    {
        Assert.True(new ConditionEvaluator().Evaluate("Path = \"C:\\\\\" and Flag",
            new Dictionary<string, object> { ["Path"] = "C:\\", ["Flag"] = true }));
    }

    [Fact]
    public void Cond_Validate_EscapedQuote_ReportsUnbalanced()
    {
        ConditionValidationResult v = new ConditionEvaluator().Validate("Name = \"say \\\"hi\"");
        bool eval = new ConditionEvaluator().Evaluate("Name = \"say \\\"hi\"", new Dictionary<string, object> { ["Name"] = "say \"hi" });
        Assert.Equal(eval, v.IsValid);
        Assert.True(eval);
    }

    #endregion

    #region Lexer escapes

    [Fact]
    public void Lexer_EscapedBackslash_ProducesSingleBackslash()
    {
        ConditionToken token = Lex("\"a\\\\b\"")[0];
        Assert.Equal(ConditionTokenType.String, token.Type);
        Assert.Equal("a\\b", token.Text);
    }

    [Fact]
    public void Lexer_EscapedQuote_ProducesQuote()
    {
        ConditionToken token = Lex("\"say \\\"hi\\\"\"")[0];
        Assert.Equal("say \"hi\"", token.Text);
    }

    [Fact]
    public void Lexer_OtherBackslash_IsKeptLiterally()
    {
        ConditionToken token = Lex("\"C:\\Temp\"")[0];
        Assert.Equal("C:\\Temp", token.Text);
    }

    [Theory]
    [InlineData("Name = \"open")]
    [InlineData("Name = \"ends with escaped quote\\\"")]
    public void Lexer_UnterminatedString_Throws(string expression)
    {
        ConditionParseException ex = Assert.Throws<ConditionParseException>(() => Lex(expression));
        Assert.True(ex.IsUnterminatedString);
    }

    [Fact]
    public void Lexer_UnterminatedSingleQuotedString_InlineDialect_Throws()
    {
        ConditionParseException ex = Assert.Throws<ConditionParseException>(
            () => new ConditionLexer(allowSingleQuotedStrings: true).Tokenize("(Name = 'open)"));
        Assert.True(ex.IsUnterminatedString);
    }

    [Fact]
    public void Evaluate_UnterminatedString_IsFalse()
    {
        Assert.False(new ConditionEvaluator().Evaluate("Name = \"abc", new Dictionary<string, object> { ["Name"] = "abc" }));
    }

    [Theory]
    [InlineData("Name = \"open")]
    [InlineData("Name = \"x\\\"")]
    public void Validate_UnterminatedString_ReportsOnlyUnbalancedQuotes(string expression)
    {
        ConditionValidationResult result = new ConditionEvaluator().Validate(expression);

        Assert.False(result.IsValid);
        ConditionValidationIssue issue = Assert.Single(result.Issues);
        Assert.Equal(ConditionValidationIssueType.UnbalancedQuotes, issue.Type);
    }

    [Fact]
    public void Validate_EscapedBackslashAtEnd_IsValid()
    {
        Assert.True(new ConditionEvaluator().Validate("Path = \"C:\\\\\"").IsValid);
    }

    #endregion

    #region Reserved words

    [Theory]
    [InlineData("Empty")]
    [InlineData("Exists")]
    [InlineData("In")]
    [InlineData("Is")]
    [InlineData("Contains")]
    [InlineData("StartsWith")]
    [InlineData("EndsWith")]
    public void BareNewKeyword_InOperandPosition_ResolvesVariable(string name)
    {
        Dictionary<string, object> data = new Dictionary<string, object> { [name] = true };
        Assert.True(new ConditionEvaluator().Evaluate(name, data));
        Assert.True(new ConditionEvaluator().Evaluate("not " + name + " = false", data));
        Assert.False(new ConditionEvaluator().Evaluate("not " + name, data));
    }

    [Fact]
    public void BareKeyword_AfterLogicalOperator_ResolvesVariable()
    {
        Dictionary<string, object> data = new Dictionary<string, object> { ["A"] = true, ["Empty"] = true };
        Assert.True(new ConditionEvaluator().Evaluate("A and Empty", data));
    }

    [Fact]
    public void BareKeyword_ComparedWithLiteral_ResolvesVariable()
    {
        Dictionary<string, object> data = new Dictionary<string, object> { ["Empty"] = "yes" };
        Assert.True(new ConditionEvaluator().Evaluate("Empty = \"yes\"", data));
    }

    [Fact]
    public void Keywords_InOperatorPosition_StillWork()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Notes"] = "",
            ["Status"] = "A",
            ["Tags"] = "x, y",
        };
        ConditionEvaluator evaluator = new ConditionEvaluator();
        Assert.True(evaluator.Evaluate("Notes is empty", data));
        Assert.True(evaluator.Evaluate("Status in (\"A\", \"B\")", data));
        Assert.True(evaluator.Evaluate("Tags contains \"x\"", data));
        Assert.True(evaluator.Evaluate("Status exists", data));
    }

    [Theory]
    [InlineData("[Empty]", "Empty")]
    [InlineData("[Not]", "Not")]
    [InlineData("[True]", "True")]
    [InlineData("[And]", "And")]
    public void BracketedIdentifier_EscapesKeyword(string expression, string name)
    {
        Assert.True(new ConditionEvaluator().Evaluate(expression, new Dictionary<string, object> { [name] = true }));
    }

    [Fact]
    public void BracketedIdentifier_WithPath_Resolves()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Empty"] = new Dictionary<string, object> { ["Name"] = "x" }
        };
        Assert.True(new ConditionEvaluator().Evaluate("[Empty].Name = \"x\"", data));
    }

    [Fact]
    public void BracketedIdentifier_LexesAsIdentifier()
    {
        ConditionToken token = Lex("[Exists]")[0];
        Assert.Equal(ConditionTokenType.Identifier, token.Type);
        Assert.Equal("Exists", token.Text);
    }

    [Fact]
    public void BareKeyword_IsMarkedOnVariableNode_BracketedIsNot()
    {
        ConditionNode bare = ConditionalEvaluator.Parse("Exists");
        ConditionNode bracketed = ConditionalEvaluator.Parse("[Exists]");

        Assert.True(Assert.IsType<VariableNode>(bare).IsBareKeyword);
        Assert.Equal("Exists", ((VariableNode)bare).Path);
        Assert.False(Assert.IsType<VariableNode>(bracketed).IsBareKeyword);
    }

    [Fact]
    public void IndexerPaths_AreUnaffectedByBracketEscape()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "a" },
            ["Settings"] = new Dictionary<string, object> { ["Theme"] = "dark" },
        };
        ConditionEvaluator evaluator = new ConditionEvaluator();
        Assert.True(evaluator.Evaluate("Items[0] = \"a\"", data));
        Assert.True(evaluator.Evaluate("Settings[Theme] = \"dark\"", data));
    }

    #endregion

    #region Warnings from the internal evaluator

    [Fact]
    public void Evaluate_WithCollector_MalformedExpression_AddsWarning()
    {
        WarningCollector collector = new WarningCollector();
        bool result = new ConditionalEvaluator().Evaluate("A &&", new GlobalEvaluationContext(new Dictionary<string, object>()), collector);

        Assert.False(result);
        ProcessingWarning warning = Assert.Single(collector.GetWarnings());
        Assert.Equal(ProcessingWarningType.ExpressionFailed, warning.Type);
    }

    [Fact]
    public void Evaluate_WithCollector_ValidExpression_AddsNoWarning()
    {
        WarningCollector collector = new WarningCollector();
        new ConditionalEvaluator().Evaluate("Missing", new GlobalEvaluationContext(new Dictionary<string, object>()), collector);

        Assert.Empty(collector.GetWarnings());
    }

    #endregion
}
