// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Shared regex patterns for conditional block detection and processing.
/// Used by both ConditionalDetector and ConditionalVisitor.
/// </summary>
internal static class ConditionalPatterns
{
    /// <summary>Pattern text of <see cref="IfStart"/> (group 1: condition).</summary>
    internal const string IfStartPattern = @"\{\{#if\s+(.+?)\}\}";

    /// <summary>Pattern text of <see cref="ElseIf"/> (group 1: condition).</summary>
    internal const string ElseIfPattern = @"\{\{#elseif\s+(.+?)\}\}";

    /// <summary>Pattern text of <see cref="Else"/>.</summary>
    internal const string ElsePattern = @"\{\{#else\}\}";

    /// <summary>Pattern text of <see cref="IfEnd"/>.</summary>
    internal const string IfEndPattern = @"\{\{/if\}\}";

    /// <summary>
    /// Pattern to match {{#if condition}} markers.
    /// </summary>
    public static readonly Regex IfStart = new(
        IfStartPattern,
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Pattern to match {{#elseif condition}} markers.
    /// </summary>
    public static readonly Regex ElseIf = new(
        ElseIfPattern,
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Pattern to match {{#else}} markers.
    /// </summary>
    public static readonly Regex Else = new(
        ElsePattern,
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Pattern to match {{/if}} markers.
    /// </summary>
    public static readonly Regex IfEnd = new(
        IfEndPattern,
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
