// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Processes test templates with <see cref="OdtTemplateProcessor"/>.
/// </summary>
internal static class OdtTestHelper
{
    /// <summary>Options with a fixed culture, so results do not depend on the machine.</summary>
    public static PlaceholderReplacementOptions InvariantOptions() =>
        new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture };

    /// <summary>
    /// Processes the template, asserts success and a valid package, and returns the result and a verifier.
    /// </summary>
    public static (ProcessingResult Result, OdtDocumentVerifier Output) Process(
        OdtDocumentBuilder template,
        Dictionary<string, object> data,
        PlaceholderReplacementOptions? options = null)
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor(options ?? InvariantOptions());
        ProcessingResult result = processor.ProcessTemplate(template.ToBytes(), data, out byte[] output);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        OdtDocumentVerifier verifier = new OdtDocumentVerifier(output);
        verifier.AssertValidOdtPackage();
        return (result, verifier);
    }

    /// <summary>Processes a single paragraph (raw XML) and returns the rendered text of the first body paragraph.</summary>
    public static string ProcessParagraphXml(string paragraphXml, Dictionary<string, object> data, PlaceholderReplacementOptions? options = null)
    {
        (_, OdtDocumentVerifier output) = Process(new OdtDocumentBuilder().AddXml(paragraphXml), data, options);
        return output.GetParagraphTexts()[0];
    }
}
