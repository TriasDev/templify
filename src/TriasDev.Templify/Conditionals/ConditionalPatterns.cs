// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Shared regex patterns for conditional block detection and processing.
/// Used by both ConditionalDetector and ConditionalVisitor.
/// </summary>
internal static partial class ConditionalPatterns
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
    public static readonly Regex IfStart = IfStartRegex();

    /// <summary>
    /// Pattern to match {{#elseif condition}} markers.
    /// </summary>
    public static readonly Regex ElseIf = ElseIfRegex();

    /// <summary>
    /// Pattern to match {{#else}} markers.
    /// </summary>
    public static readonly Regex Else = ElseRegex();

    /// <summary>
    /// Pattern to match {{/if}} markers.
    /// </summary>
    public static readonly Regex IfEnd = IfEndRegex();

    [GeneratedRegex(IfStartPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IfStartRegex();

    [GeneratedRegex(ElseIfPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ElseIfRegex();

    [GeneratedRegex(ElsePattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ElseRegex();

    [GeneratedRegex(IfEndPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IfEndRegex();
}
