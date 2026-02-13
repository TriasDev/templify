// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Formatting;
using TriasDev.Templify.Replacements;

namespace TriasDev.Templify.Core;

/// <summary>
/// Configuration options for placeholder replacement in document templates.
/// </summary>
public sealed class PlaceholderReplacementOptions
{
    /// <summary>
    /// Gets or initializes the behavior for handling missing variables.
    /// Default is <see cref="MissingVariableBehavior.LeaveUnchanged"/>.
    /// </summary>
    public MissingVariableBehavior MissingVariableBehavior { get; init; } = MissingVariableBehavior.LeaveUnchanged;

    /// <summary>
    /// Gets or initializes the culture used for formatting numbers, dates, and other culture-sensitive values.
    /// Default is <see cref="CultureInfo.CurrentCulture"/>.
    /// Use <see cref="CultureInfo.InvariantCulture"/> for culture-independent formatting.
    /// </summary>
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;

    /// <summary>
    /// Gets or initializes the boolean formatter registry for custom boolean display formats.
    /// If null, a default registry with culture-aware formatters will be created automatically.
    /// </summary>
    public BooleanFormatterRegistry? BooleanFormatterRegistry { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether newline characters (\n, \r\n, \r) in variable values
    /// should be converted to line breaks in the Word document.
    /// Default is true.
    /// </summary>
    public bool EnableNewlineSupport { get; init; } = true;

    /// <summary>
    /// Gets or initializes a value indicating whether validation should warn about empty loop collections.
    /// When true, empty collections produce a warning indicating that variables inside the loop could not be validated.
    /// When false, empty collections are silently accepted without warnings.
    /// Default is true.
    /// </summary>
    public bool WarnOnEmptyLoopCollections { get; init; } = true;

    /// <summary>
    /// Gets or initializes a dictionary of text replacements to apply to variable values before processing.
    /// Use this to convert HTML entities, custom placeholders, or other text patterns.
    /// Default is null (no replacements).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replacements are applied after value conversion but before newline and markdown processing.
    /// This allows HTML line breaks (e.g., &lt;br&gt;) to be converted to \n, which then gets
    /// processed into Word line breaks.
    /// </para>
    /// <para>
    /// Use the built-in <see cref="TextReplacements.HtmlEntities"/> preset for common HTML entities:
    /// </para>
    /// <code>
    /// var options = new PlaceholderReplacementOptions
    /// {
    ///     TextReplacements = TextReplacements.HtmlEntities
    /// };
    /// </code>
    /// <para>
    /// Or define custom replacements:
    /// </para>
    /// <code>
    /// var options = new PlaceholderReplacementOptions
    /// {
    ///     TextReplacements = new Dictionary&lt;string, string&gt;
    ///     {
    ///         ["&lt;br&gt;"] = "\n",
    ///         ["COMPANY_NAME"] = "Acme Corp"
    ///     }
    /// };
    /// </code>
    /// </remarks>
    public IReadOnlyDictionary<string, string>? TextReplacements { get; init; }

    /// <summary>
    /// Gets or initializes when Word should update all fields (including Table of Contents)
    /// when the document is first opened after processing.
    /// Default is <see cref="UpdateFieldsOnOpenMode.Never"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When template processing removes or adds content (via conditionals or loops), fields like
    /// Table of Contents (TOC) contain stale page numbers.
    /// </para>
    /// <para>
    /// Available modes:
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="UpdateFieldsOnOpenMode.Never"/> - Never prompt to update fields (default)</description></item>
    /// <item><description><see cref="UpdateFieldsOnOpenMode.Always"/> - Always prompt to update fields</description></item>
    /// <item><description><see cref="UpdateFieldsOnOpenMode.Auto"/> - Only prompt if document contains fields (TOC, PAGE, etc.). Recommended for applications processing various templates.</description></item>
    /// </list>
    /// <para>
    /// Note: When enabled, Word will display a prompt asking the user to confirm
    /// field updates. This is a security measure built into Word.
    /// </para>
    /// </remarks>
    public UpdateFieldsOnOpenMode UpdateFieldsOnOpen { get; init; } = UpdateFieldsOnOpenMode.Never;

    /// <summary>
    /// Gets or initializes the document metadata properties to set on the output document.
    /// When null (default), the original template properties are preserved unchanged.
    /// Only non-null property values within <see cref="DocumentProperties"/> are applied;
    /// properties left as null preserve the original template value.
    /// </summary>
    public DocumentProperties? DocumentProperties { get; init; }

    /// <summary>
    /// Creates a new instance of <see cref="PlaceholderReplacementOptions"/> with default settings.
    /// </summary>
    public PlaceholderReplacementOptions()
    {
    }
}
