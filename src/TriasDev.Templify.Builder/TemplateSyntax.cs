// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// The one place that writes Templify syntax. Writers ask it for the text of placeholders and markers and only
/// decide where that text goes.
/// </summary>
internal static class TemplateSyntax
{
    /// <summary>The placeholder for a value, for example <c>{{customer.name}}</c>.</summary>
    /// <param name="path">A non-empty data path.</param>
    public static string Placeholder(TemplatePath path) => "{{" + path + "}}";
}
