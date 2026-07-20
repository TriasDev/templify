// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class ConditionEvaluatorCoreTests
{
    private static ConditionEvaluatorCore Default(Dictionary<string, object> data)
        => new(new GlobalEvaluationContext(data), DefaultConditionDialect.Instance);

    private static readonly ConditionOperatorRegistry _reg = ConditionOperatorRegistry.Shared;

    [Fact]
    public void Equal_MatchingStrings_IsTrue()
    {
        var node = new OperatorNode(_reg.FindInfix("=")!,
            new ConditionNode[] { new VariableNode("Status"), new LiteralNode("Active") });
        Assert.True(Default(new() { ["Status"] = "Active" }).EvaluateBool(node));
    }

    [Fact]
    public void And_ShortCircuits_And_Combines()
    {
        var left = new OperatorNode(_reg.FindInfix(">")!,
            new ConditionNode[] { new VariableNode("Count"), new LiteralNode(0) });
        var node = new OperatorNode(_reg.FindInfix("and")!,
            new ConditionNode[] { left, new VariableNode("IsOn") });
        Assert.True(Default(new() { ["Count"] = 5, ["IsOn"] = true }).EvaluateBool(node));
        Assert.False(Default(new() { ["Count"] = 0, ["IsOn"] = true }).EvaluateBool(node));
    }

    [Fact]
    public void Not_NegatesOperand()
    {
        var node = new OperatorNode(_reg.FindPrefix("not")!,
            new ConditionNode[] { new VariableNode("IsOff") });
        Assert.True(Default(new() { ["IsOff"] = false }).EvaluateBool(node));
    }

    [Fact]
    public void Greater_UsesNumericComparison()
    {
        var node = new OperatorNode(_reg.FindInfix(">")!,
            new ConditionNode[] { new VariableNode("Count"), new LiteralNode(3) });
        Assert.True(Default(new() { ["Count"] = 5 }).EvaluateBool(node));
    }
}
