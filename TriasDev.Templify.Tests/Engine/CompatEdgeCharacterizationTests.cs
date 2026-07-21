// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

/// <summary>
/// Characterization tests that pin the CURRENT (post-migration) behavior of edge cases where the new
/// expression engine intentionally differs from the legacy <c>ConditionalEvaluator</c>. These are
/// accepted, deliberate changes (F2/F3/F4); the tests exist to lock the behavior against regressions.
/// </summary>
public class CompatEdgeCharacterizationTests
{
    private static bool EvalInline(string expr, Dictionary<string, object> data)
    {
        IReadOnlyList<ConditionToken> tokens = new ConditionLexer().Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), InlineConditionDialect.Instance)
            .EvaluateBool(node);
    }

    // F2: In the inline dialect a variable-to-variable comparison now resolves the RHS as a variable
    // (rather than treating it as an opaque literal). This is an intentional change from the legacy engine.
    [Fact]
    public void F2_Inline_VariableToVariableEquality_ResolvesRhs_IsTrue()
    {
        Assert.True(EvalInline("(A = B)", new() { ["A"] = "x", ["B"] = "x" }));
    }

    // F3: In the inline dialect, an ordered comparison between incomparable operands yields false
    // (IComparable.CompareTo throws for the mismatched types, and TryCompare maps that to false).
    // Intentional change: incomparable => false rather than an error.
    [Fact]
    public void F3_Inline_IncomparableGreaterOrEqual_IsFalse()
    {
        Assert.False(EvalInline("(Amount >= \"abc\")", new() { ["Amount"] = 10 }));
    }

    // F4: The operator keywords are reserved. A quoted literal still compares as a string...
    [Fact]
    public void F4_Default_QuotedReservedWord_ComparesAsLiteral_IsTrue()
    {
        Assert.True(new ConditionEvaluator().Evaluate(
            "Category = \"empty\"", new Dictionary<string, object> { ["Category"] = "empty" }));
    }

    // ...but the same word UNQUOTED is parsed as the reserved `empty` keyword, which is not a valid
    // right-hand operand — so the expression fails to parse and Evaluate returns false. Intentional
    // change: unquoted reserved words are no longer treated as bareword string literals.
    [Fact]
    public void F4_Default_UnquotedReservedWord_IsNotLiteral_IsFalse()
    {
        Assert.False(new ConditionEvaluator().Evaluate(
            "Category = empty", new Dictionary<string, object> { ["Category"] = "empty" }));
    }
}
