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
    /// Default is <see cref="CultureInfo.CurrentCulture"/>, captured when the options object is created.
    /// Use <see cref="CultureInfo.InvariantCulture"/> for culture-independent formatting.
    /// </summary>
    /// <remarks>
    /// On a server, <see cref="CultureInfo.CurrentCulture"/> is the culture of the process or request thread,
    /// so the same template and data can produce different output (decimal separators, date formats, month
    /// names, localized boolean formats) on differently configured machines. Set this property explicitly,
    /// e.g. <c>CultureInfo.GetCultureInfo("en-US")</c> or <see cref="CultureInfo.InvariantCulture"/>, for
    /// reproducible output.
    /// </remarks>
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;

    /// <summary>
    /// Gets or initializes the boolean formatter registry for custom boolean display formats.
    /// If null, a default registry with culture-aware formatters will be created automatically.
    /// </summary>
    /// <remarks>
    /// <see cref="Formatting.BooleanFormatterRegistry"/> is not thread-safe for writes: register all custom
    /// formatters before assigning the registry here, and do not call
    /// <see cref="Formatting.BooleanFormatterRegistry.Register(string, Formatting.BooleanFormatter)"/> while
    /// templates that use these options are being processed. A fully registered registry can be shared by
    /// concurrent processing calls.
    /// </remarks>
    public BooleanFormatterRegistry? BooleanFormatterRegistry { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether newline characters (\n, \r\n, \r) in variable values
    /// should be converted to line breaks in the Word document.
    /// Default is true.
    /// </summary>
    public bool EnableNewlineSupport { get; init; } = true;

    /// <summary>
    /// Gets or initializes a value indicating whether markdown syntax in variable values
    /// should be rendered as Word formatting (bold, italic, strikethrough).
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When enabled, any value containing <c>*</c>, <c>_</c> or <c>~</c> may be interpreted as markdown:
    /// <c>**text**</c>/<c>__text__</c> become bold, <c>*text*</c>/<c>_text_</c> italic and
    /// <c>~~text~~</c> strikethrough, and the markers are removed. This also affects ordinary data
    /// such as <c>my_report_final.docx</c> (rendered as <c>myreportfinal.docx</c> with "report" in italic)
    /// or <c>2*3*4</c> (rendered as <c>234</c>).
    /// </para>
    /// <para>
    /// When disabled, values are inserted as plain text. Newline handling
    /// (<see cref="EnableNewlineSupport"/>) is unaffected.
    /// </para>
    /// <para>
    /// Individual placeholders can opt out of markdown while this option is enabled by using the
    /// <c>:raw</c> format specifier, e.g. <c>{{FileName:raw}}</c>.
    /// </para>
    /// </remarks>
    public bool EnableMarkdown { get; init; } = true;

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
