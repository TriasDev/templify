// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Binding precedence levels of the condition operators (higher binds tighter).</summary>
internal static class OperatorPrecedence
{
    /// <summary><c>or</c>.</summary>
    public const int Or = 1;

    /// <summary><c>and</c>.</summary>
    public const int And = 2;

    /// <summary>Prefix <c>not</c>.</summary>
    public const int Not = 3;

    /// <summary>Comparison, membership and string operators (<c>=</c>, <c>&gt;</c>, <c>in</c>, <c>contains</c>, …).</summary>
    public const int Comparison = 4;

    /// <summary>Postfix existence/emptiness operators (<c>exists</c>, <c>is empty</c>, <c>is not empty</c>).</summary>
    public const int Postfix = 5;
}
