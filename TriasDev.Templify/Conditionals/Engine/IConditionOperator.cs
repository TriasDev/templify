// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Contract for operator implementations in the condition-expression engine.</summary>
/// <remarks>
/// This interface is implemented by built-in operators (equality, comparison, logical, etc).
/// The <c>Evaluate</c> method is added in Task 3 when the evaluator core is available.
/// </remarks>
internal interface IConditionOperator
{
    /// <summary>The token strings that invoke this operator (e.g., <c>"=", "==" for equality</c>).</summary>
    IReadOnlyList<string> Tokens { get; }

    /// <summary>Operator precedence (higher number binds tighter).</summary>
    int Precedence { get; }

    /// <summary>Where this operator sits relative to its operands (prefix, infix, postfix).</summary>
    OperatorFixity Fixity { get; }

    // NOTE: Evaluate(...) method added in Task 3
}
