// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO.Compression;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Cleans up Word documents by removing all SDT (Structured Document Tag) elements
/// while preserving their content. This fixes documents that have orphaned or
/// malformed SDT elements that prevent Word from opening them.
/// </summary>
public static class DocumentCleaner
{
    /// <summary>
    /// Removes all SDT elements from a Word document while preserving their content.
    /// This is useful after template conversion to ensure the document can be opened in Word.
    /// </summary>
    /// <param name="document">The Word document to clean.</param>
    /// <returns>The number of SDT elements that were removed.</returns>
    public static int RemoveAllSdtElements(WordprocessingDocument document)
    {
        if (document.MainDocumentPart?.Document?.Body == null)
        {
            return 0;
        }

        int removedCount = 0;

        // Process all SDT elements in the document
        // We need to iterate multiple times because removing parent SDTs
        // may expose child SDTs that weren't visible in the first pass
        bool foundSdts;
        do
        {
            foundSdts = false;

            // Find all SdtElement descendants (includes SdtBlock, SdtRun, SdtCell, etc.)
            List<SdtElement> sdtElements = document.MainDocumentPart.Document.Descendants<SdtElement>().ToList();

            if (sdtElements.Count > 0)
            {
                foundSdts = true;
                removedCount += sdtElements.Count;

                // Process each SDT element
                foreach (SdtElement sdt in sdtElements)
                {
                    UnwrapSdtElement(sdt);
                }
            }
        }
        while (foundSdts);

        return removedCount;
    }

    /// <summary>
    /// Unwraps a single SDT element, moving its content to replace the SDT.
    /// </summary>
    /// <param name="sdt">The SDT element to unwrap.</param>
    private static void UnwrapSdtElement(SdtElement sdt)
    {
        // Skip if already removed
        if (sdt.Parent == null)
        {
            return;
        }

        // Get the content element
        OpenXmlCompositeElement? sdtContent = GetSdtContent(sdt);

        if (sdtContent == null)
        {
            // No content, just remove the SDT
            sdt.Remove();
            return;
        }

        // Get all children of the content element
        List<OpenXmlElement> children = sdtContent.ChildElements.ToList();

        if (children.Count == 0)
        {
            // No children, just remove the SDT
            sdt.Remove();
            return;
        }

        // Insert all children before the SDT
        foreach (OpenXmlElement child in children)
        {
            // Clone the child to avoid removing it from its current parent
            OpenXmlElement clonedChild = child.CloneNode(true);
            sdt.InsertBeforeSelf(clonedChild);
        }

        // Remove the SDT wrapper
        sdt.Remove();
    }

    /// <summary>
    /// Gets the content element from an SdtElement (handles different SDT types).
    /// </summary>
    /// <param name="sdt">The SDT element.</param>
    /// <returns>The content element, or null if not found.</returns>
    private static OpenXmlCompositeElement? GetSdtContent(SdtElement sdt)
    {
        if (sdt is SdtBlock block)
        {
            return block.SdtContentBlock;
        }
        else if (sdt is SdtRun run)
        {
            return run.SdtContentRun;
        }
        else if (sdt is SdtCell cell)
        {
            return cell.SdtContentCell;
        }
        else if (sdt is SdtRow row)
        {
            return row.SdtContentRow;
        }

        return null;
    }

    /// <summary>
    /// Cleans a Word document file by removing all SDT elements.
    /// </summary>
    /// <param name="inputPath">Path to the input document.</param>
    /// <param name="outputPath">Path to save the cleaned document. If null, overwrites the input.</param>
    /// <returns>The number of SDT elements that were removed.</returns>
    public static int CleanDocument(string inputPath, string? outputPath = null)
    {
        if (outputPath == null)
        {
            outputPath = inputPath;
        }

        // If input and output are the same, work on a copy first
        string workingPath = inputPath;
        if (inputPath == outputPath)
        {
            workingPath = inputPath + ".temp";
            File.Copy(inputPath, workingPath, overwrite: true);
        }
        else
        {
            File.Copy(inputPath, outputPath, overwrite: true);
            workingPath = outputPath;
        }

        int removedCount;

        try
        {
            // Open and clean the document
            using (WordprocessingDocument document = WordprocessingDocument.Open(workingPath, isEditable: true))
            {
                removedCount = RemoveAllSdtElements(document);
                document.MainDocumentPart?.Document.Save();
            }

            // If we were working on a temp file, replace the original
            if (workingPath != outputPath)
            {
                File.Move(workingPath, outputPath, overwrite: true);
            }

            // Fix ZIP permissions issue (macOS/Linux compatibility)
            FixZipPermissions(outputPath);
        }
        catch
        {
            // Clean up temp file on error
            if (workingPath != outputPath && File.Exists(workingPath))
            {
                File.Delete(workingPath);
            }
            throw;
        }

        return removedCount;
    }

    /// <summary>
    /// Fixes ZIP archive file permissions that may be corrupted by the OpenXML SDK.
    /// On macOS and some Linux systems, the DocumentFormat.OpenXml library creates ZIP archives
    /// with invalid file permissions (000000 octal), preventing Word from opening the file.
    /// This method repacks the ZIP archive with proper permissions.
    /// </summary>
    /// <param name="docxPath">Path to the .docx file to fix.</param>
    private static void FixZipPermissions(string docxPath)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Extract the entire ZIP archive
            ZipFile.ExtractToDirectory(docxPath, tempDir);

            // Delete the original file
            File.Delete(docxPath);

            // Recreate the ZIP archive with proper permissions
            // System.IO.Compression.ZipFile automatically sets correct permissions
            ZipFile.CreateFromDirectory(tempDir, docxPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        finally
        {
            // Clean up temporary directory
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
