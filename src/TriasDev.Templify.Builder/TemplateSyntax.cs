// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// The one place that writes Templify syntax. Writers ask it for the text of placeholders and markers and only
/// decide where that text goes.
/// </summary>
internal static class TemplateSyntax
{
    /// <summary>The placeholder for a value, for example <c>{{customer.name}}</c> or <c>{{order.date:date:yyyy-MM-dd}}</c>.</summary>
    /// <param name="path">A non-empty data path.</param>
    /// <param name="format">The format specifier, or null.</param>
    public static string Placeholder(TemplatePath path, ValueFormat? format = null) =>
        format is null ? "{{" + path + "}}" : "{{" + path + ":" + format.Specifier + "}}";

    /// <summary>The else marker.</summary>
    public const string Else = "{{#else}}";

    /// <summary>The end marker of a conditional.</summary>
    public const string EndIf = "{{/if}}";

    /// <summary>The start marker of a conditional, for example <c>{{#if order.total &gt; 100}}</c>.</summary>
    public static string If(Condition condition) => "{{#if " + condition + "}}";

    /// <summary>An elseif marker.</summary>
    public static string ElseIf(Condition condition) => "{{#elseif " + condition + "}}";
}
