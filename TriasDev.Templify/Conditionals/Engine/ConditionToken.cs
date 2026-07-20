// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>The kind of a lexed condition token.</summary>
internal enum ConditionTokenType
{
    Identifier,
    String,
    Number,
    Boolean,
    Null,
    Operator,
    LParen,
    RParen,
    Comma,
    End
}

/// <summary>A single lexed token.</summary>
internal sealed class ConditionToken
{
    public ConditionToken(ConditionTokenType type, string text, object? literalValue = null)
    {
        Type = type;
        Text = text;
        LiteralValue = literalValue;
    }

    public ConditionTokenType Type { get; }

    public string Text { get; }

    public object? LiteralValue { get; }
}
