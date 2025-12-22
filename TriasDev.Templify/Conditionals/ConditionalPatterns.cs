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
    /// <summary>
    /// Pattern to match {{#if condition}} markers.
    /// </summary>
    public static readonly Regex IfStart = new(
        @"\{\{#if\s+(.+?)\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Pattern to match {{#elseif condition}} markers.
    /// </summary>
    public static readonly Regex ElseIf = new(
        @"\{\{#elseif\s+(.+?)\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Pattern to match {{else}} markers.
    /// </summary>
    public static readonly Regex Else = new(
        @"\{\{else\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Pattern to match {{/if}} markers.
    /// </summary>
    public static readonly Regex IfEnd = new(
        @"\{\{/if\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
}
