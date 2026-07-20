// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class ExistenceOperatorsTests
{
    private static bool Eval(string expr, Dictionary<string, object> data)
    {
        var tokens = new ConditionLexer().Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), DefaultConditionDialect.Instance).EvaluateBool(node);
    }

    [Fact]
    public void Exists_PresentVariable_IsTrue()
        => Assert.True(Eval("Notes exists", new() { ["Notes"] = "x" }));

    [Fact]
    public void Exists_MissingVariable_IsFalse()
        => Assert.False(Eval("Notes exists", new()));

    [Fact]
    public void IsEmpty_EmptyString_IsTrue()
        => Assert.True(Eval("Notes is empty", new() { ["Notes"] = "  " }));

    [Fact]
    public void IsEmpty_EmptyCollection_IsTrue()
        => Assert.True(Eval("Items is empty", new() { ["Items"] = new List<object>() }));

    [Fact]
    public void IsEmpty_MissingVariable_IsTrue()
        => Assert.True(Eval("Notes is empty", new()));

    [Fact]
    public void IsNotEmpty_NonEmpty_IsTrue()
        => Assert.True(Eval("Notes is not empty", new() { ["Notes"] = "x" }));

    [Fact]
    public void PresentButNull_ExistsFalse_IsEmptyTrue()
    {
        var data = new Dictionary<string, object?> { ["Notes"] = null };
        var ctx = new GlobalEvaluationContext(data!);
        ConditionNode existsNode = Build("Notes exists");
        ConditionNode emptyNode = Build("Notes is empty");
        // 'Notes' resolves (key present) → exists true; value null → is empty true.
        Assert.True(new ConditionEvaluatorCore(ctx, DefaultConditionDialect.Instance).EvaluateBool(existsNode));
        Assert.True(new ConditionEvaluatorCore(ctx, DefaultConditionDialect.Instance).EvaluateBool(emptyNode));
    }

    private static ConditionNode Build(string expr)
        => new ConditionParser(ConditionOperatorRegistry.Shared).Parse(new ConditionLexer().Tokenize(expr));
}
