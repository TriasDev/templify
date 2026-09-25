// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Tests.Engine;

/// <summary>
/// Tests for the parsed-expression cache (issue #155).
/// </summary>
public sealed class ConditionAstCacheTests
{
    private static ConditionNode ParseCondition(string expression) =>
        new ConditionParser(ConditionOperatorRegistry.Shared).Parse(new ConditionLexer().Tokenize(expression));

    [Fact]
    public void GetOrParse_SameExpression_ParsesOnce()
    {
        int parses = 0;
        ConditionAstCache cache = new ConditionAstCache(e => { parses++; return ParseCondition(e); });

        ConditionNode first = cache.GetOrParse("A > 1 and B");
        ConditionNode second = cache.GetOrParse("A > 1 and B");

        Assert.Same(first, second);
        Assert.Equal(1, parses);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrParse_MalformedExpression_ThrowsEveryTimeAndIsNotCached()
    {
        ConditionAstCache cache = new ConditionAstCache(ParseCondition);

        Assert.Throws<ConditionParseException>(() => cache.GetOrParse("A and"));
        Assert.Throws<ConditionParseException>(() => cache.GetOrParse("A and"));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void GetOrParse_CapacityReached_ClearsAndStaysBounded()
    {
        ConditionAstCache cache = new ConditionAstCache(ParseCondition, capacity: 4);

        for (int i = 0; i < 20; i++)
        {
            cache.GetOrParse($"A = {i}");
            Assert.InRange(cache.Count, 1, 4);
        }
    }

    [Fact]
    public void GetOrParse_IsCaseAndQuoteSensitive()
    {
        ConditionAstCache cache = new ConditionAstCache(ParseCondition);

        cache.GetOrParse("Name = \"a\"");
        cache.GetOrParse("Name = \"A\"");

        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void GetOrParse_ConcurrentCallers_GetEquivalentResults()
    {
        ConditionAstCache cache = new ConditionAstCache(ParseCondition, capacity: 8);
        Dictionary<string, object> data = new Dictionary<string, object> { ["A"] = 5 };

        Parallel.For(0, 1000, i =>
        {
            int threshold = i % 16;
            ConditionNode node = cache.GetOrParse($"A > {threshold}");
            bool result = new ConditionEvaluatorCore(new GlobalEvaluationContext(data), DefaultConditionDialect.Instance).EvaluateBool(node);
            Assert.Equal(5 > threshold, result);
        });
    }

    [Fact]
    public void Conditions_And_InlineExpressions_AreSeparate()
    {
        // Single quotes delimit strings only in inline expressions; the caches must not share entries.
        Dictionary<string, object> data = new Dictionary<string, object> { ["Name"] = "x" };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        bool inlineOk = ExpressionPlaceholderEvaluator.TryEvaluate("(Name = 'x')", context, new WarningCollector(), out object? inlineValue);
        bool condition = new ConditionalEvaluator().Evaluate("(Name = 'x')", context);

        Assert.True(inlineOk);
        Assert.Equal(true, inlineValue);
        Assert.False(condition);
    }

    [Fact]
    public void ValueConverter_DefaultBooleanFormatters_ArePerLanguage()
    {
        Assert.Equal("Ja", ValueConverter.ConvertToString(true, new CultureInfo("de-DE"), "yesno", null));
        Assert.Equal("Ja", ValueConverter.ConvertToString(true, new CultureInfo("de-AT"), "yesno", null));
        Assert.Equal("Yes", ValueConverter.ConvertToString(true, CultureInfo.InvariantCulture, "yesno", null));
        Assert.Equal("Oui", ValueConverter.ConvertToString(true, new CultureInfo("fr-FR"), "yesno", null));
        Assert.Equal("Ja", ValueConverter.ConvertToString(true, new CultureInfo("de-DE"), "yesno", null));
    }
}
