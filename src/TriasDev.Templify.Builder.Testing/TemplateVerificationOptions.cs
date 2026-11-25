// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Testing;

/// <summary>What <see cref="TemplateVerifier"/> builds and checks.</summary>
public sealed class TemplateVerificationOptions
{
    /// <summary>Gets the languages to build in. Default: en-US.</summary>
    public IReadOnlyList<CultureInfo> Languages { get; init; } = [CultureInfo.GetCultureInfo("en-US")];

    /// <summary>Gets the formats to build. Default: DOCX and ODT.</summary>
    public IReadOnlyList<TemplateFormat> Formats { get; init; } = [TemplateFormat.Docx, TemplateFormat.Odt];

    /// <summary>Gets the text catalog for a language, or null for <see cref="TextCatalog.Empty"/>.</summary>
    public Func<CultureInfo, ITextCatalog>? Texts { get; init; }

    /// <summary>
    /// Gets sample data documents (JSON). Every template is filled with each of them; the fill must succeed without
    /// missing variables and without warnings.
    /// </summary>
    public IReadOnlyList<string> SampleData { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether all languages and formats must have the same data contract (the language parity
    /// check). Default: true.
    /// </summary>
    public bool RequireEqualContracts { get; init; } = true;

    /// <summary>Gets a value indicating whether build warnings (such as TB9001) fail the verification. Default: false.</summary>
    public bool FailOnBuildWarnings { get; init; }

    /// <summary>
    /// Gets the current cultures under which a second build must give the same bytes. Default: tr-TR and ar-SA, the
    /// cultures that break culture-sensitive formatting and casing.
    /// </summary>
    public IReadOnlyList<CultureInfo> DeterminismCultures { get; init; } =
        [CultureInfo.GetCultureInfo("tr-TR"), CultureInfo.GetCultureInfo("ar-SA")];
}
