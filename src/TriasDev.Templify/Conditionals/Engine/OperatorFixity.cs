// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Where an operator sits relative to its operands.</summary>
internal enum OperatorFixity
{
    Prefix,
    Infix,
    Postfix
}
