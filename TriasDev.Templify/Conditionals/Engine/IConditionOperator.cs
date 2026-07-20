// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>
/// A pluggable condition operator. Adding a new operator means implementing this and
/// registering it in <see cref="ConditionOperatorRegistry"/>; the parser is not modified.
/// </summary>
internal interface IConditionOperator
{
    /// <summary>Token sequence that denotes this operator (e.g. ["="], ["is","empty"]).</summary>
    IReadOnlyList<string> Tokens { get; }

    /// <summary>Binding precedence (higher binds tighter).</summary>
    int Precedence { get; }

    /// <summary>Operator position relative to its operands.</summary>
    OperatorFixity Fixity { get; }

    /// <summary>Evaluates the operator against its operand nodes.</summary>
    bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands);
}
