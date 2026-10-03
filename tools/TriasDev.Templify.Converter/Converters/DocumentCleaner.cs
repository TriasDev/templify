// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Cleans up Word documents by removing SDT (Structured Document Tag / content control) wrappers
/// while preserving their content. Processes the body, headers, footers, footnotes and endnotes.
/// </summary>
public static class DocumentCleaner
{
    /// <summary>
    /// Removes all SDT elements from a Word document while preserving their content.
    /// </summary>
    /// <param name="document">The Word document to clean.</param>
    /// <returns>The number of SDT elements that were removed.</returns>
    public static int RemoveAllSdtElements(WordprocessingDocument document)
    {
        return RemoveSdtElements(document, _ => true);
    }

    /// <summary>
    /// Removes the SDT elements matching <paramref name="predicate"/> while preserving their content.
    /// </summary>
    /// <param name="document">The Word document to clean.</param>
    /// <param name="predicate">Selects the controls to unwrap.</param>
    /// <returns>The number of SDT elements that were removed.</returns>
    public static int RemoveSdtElements(WordprocessingDocument document, Func<SdtElement, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(predicate);

        int removedCount = 0;

        foreach (OpenXmlPartRootElement root in OpenXmlHelpers.GetContentRoots(document))
        {
            // Snapshot once. Unwrapping moves (does not clone) children, so nested controls stay
            // valid references and every control is counted exactly once.
            List<SdtElement> sdtElements = root.Descendants<SdtElement>().Where(predicate).ToList();
            if (sdtElements.Count == 0)
            {
                continue;
            }

            foreach (SdtElement sdt in sdtElements)
            {
                if (sdt.Parent == null)
                {
                    continue;
                }

                OpenXmlHelpers.UnwrapContentControl(sdt);
                removedCount++;
            }

            root.Save();
        }

        return removedCount;
    }

    /// <summary>
    /// Cleans a Word document file by removing all SDT elements. The input file is never modified
    /// directly: the work is done on a temporary copy that replaces the output only on success.
    /// </summary>
    /// <param name="inputPath">Path to the input document.</param>
    /// <param name="outputPath">Path to save the cleaned document. If null, overwrites the input.</param>
    /// <returns>The number of SDT elements that were removed.</returns>
    public static int CleanDocument(string inputPath, string? outputPath = null)
    {
        outputPath ??= inputPath;

        return SafeFileWriter.Write(inputPath, outputPath, workingPath =>
        {
            using WordprocessingDocument document = WordprocessingDocument.Open(workingPath, isEditable: true);
            return RemoveAllSdtElements(document);
        });
    }
}
