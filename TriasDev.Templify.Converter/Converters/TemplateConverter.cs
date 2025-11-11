using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO.Compression;
using System.IO.Packaging;
using TriasDev.Templify.Converter.Models;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Main converter that orchestrates the conversion from OpenXMLTemplates to Templify.
/// </summary>
public class TemplateConverter
{
    private readonly VariableConverter _variableConverter = new();
    private readonly ConditionalConverter _conditionalConverter = new();
    private readonly RepeatingConverter _repeatingConverter = new();

    /// <summary>
    /// Convert a template from OpenXMLTemplates to Templify.
    /// </summary>
    /// <param name="inputPath">Path to the original template.</param>
    /// <param name="outputPath">Path where the converted template will be saved.</param>
    /// <returns>Conversion result with statistics and any issues.</returns>
    public ConversionResult ConvertTemplate(string inputPath, string outputPath)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Template file not found: {inputPath}");
        }

        ConversionResult result = new ConversionResult();

        // Copy template to output
        File.Copy(inputPath, outputPath, overwrite: true);

        // Open the copied template for editing
        using (WordprocessingDocument document = WordprocessingDocument.Open(outputPath, true))
        {
            if (document.MainDocumentPart == null)
            {
                throw new InvalidOperationException("Document has no main document part");
            }

            // Find all content controls
            List<SdtElement> contentControls = document.MainDocumentPart.Document.Body
                .Descendants<SdtElement>()
                .Where(sdt => OpenXmlHelpers.GetContentControlTag(sdt) != null)
                .ToList();

            result.TotalControls = contentControls.Count;

            Console.WriteLine($"Found {contentControls.Count} content controls to convert");

            // Process controls in reverse order to handle nested controls properly
            // (convert inner controls before outer controls)
            List<SdtElement> sortedControls = SortControlsByDepth(contentControls);

            int converted = 0;
            int skipped = 0;

            foreach (SdtElement sdt in sortedControls)
            {
                string? tag = OpenXmlHelpers.GetContentControlTag(sdt);
                if (tag == null)
                {
                    skipped++;
                    continue;
                }

                bool success = false;

                try
                {
                    // Determine control type and convert
                    if (tag.StartsWith("variable_"))
                    {
                        success = _variableConverter.Convert(sdt, tag);
                        if (success)
                        {
                            IncrementConversionCount(result, ControlType.Variable);
                        }
                    }
                    else if (tag.StartsWith("conditionalRemove_"))
                    {
                        success = _conditionalConverter.Convert(sdt, tag);
                        if (success)
                        {
                            IncrementConversionCount(result, ControlType.Conditional);
                        }
                    }
                    else if (tag.StartsWith("repeating_"))
                    {
                        success = _repeatingConverter.Convert(sdt, tag);
                        if (success)
                        {
                            IncrementConversionCount(result, ControlType.Repeating);
                        }
                    }
                    else
                    {
                        result.Warnings.Add($"Unknown control type: {tag}");
                        skipped++;
                        continue;
                    }

                    if (success)
                    {
                        converted++;
                    }
                    else
                    {
                        skipped++;
                        result.Warnings.Add($"Failed to convert control: {tag}");
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error converting {tag}: {ex.Message}");
                    result.FailedConversions.Add(new ControlInfo
                    {
                        Tag = tag,
                        Notes = new List<string> { ex.Message }
                    });
                    skipped++;
                }
            }

            result.ConvertedControls = converted;
            result.SkippedControls = skipped;

            // Clean up any remaining SDT elements to ensure Word compatibility
            Console.WriteLine("Cleaning up remaining SDT elements...");
            int cleanedCount = DocumentCleaner.RemoveAllSdtElements(document);
            result.CleanedSdtElements = cleanedCount;

            if (cleanedCount > 0)
            {
                Console.WriteLine($"Removed {cleanedCount} SDT element(s) during cleanup");
            }
            else
            {
                Console.WriteLine("No SDT elements needed cleanup");
            }

            // Fix TableLook elements to use correct schema
            FixTableLookElements(document);

            // Save changes
            document.MainDocumentPart.Document.Save();
        }

        // Fix ZIP permissions issue (macOS/Linux compatibility)
        // The OpenXML SDK on some platforms creates ZIP archives with invalid permissions (000000)
        // This causes Word to fail opening the file with "Word experienced an error" message
        Console.WriteLine("Fixing ZIP archive permissions...");
        FixZipPermissions(outputPath);
        Console.WriteLine("ZIP permissions fixed");

        return result;
    }

    /// <summary>
    /// Sort controls by depth (deepest first) to handle nested controls properly.
    /// </summary>
    private List<SdtElement> SortControlsByDepth(List<SdtElement> controls)
    {
        // Calculate depth for each control (number of ancestors)
        return controls
            .OrderByDescending(sdt => GetDepth(sdt))
            .ToList();
    }

    /// <summary>
    /// Get the depth of an element (number of ancestor elements).
    /// </summary>
    private int GetDepth(OpenXmlElement element)
    {
        int depth = 0;
        OpenXmlElement? current = element.Parent;

        while (current != null)
        {
            depth++;
            current = current.Parent;
        }

        return depth;
    }

    /// <summary>
    /// Increment the conversion count for a specific type.
    /// </summary>
    private void IncrementConversionCount(ConversionResult result, ControlType type)
    {
        if (!result.ConversionsByType.ContainsKey(type))
        {
            result.ConversionsByType[type] = 0;
        }

        result.ConversionsByType[type]++;
    }

    /// <summary>
    /// Fixes ZIP archive file permissions and timestamps that may be corrupted by the OpenXML SDK.
    /// On macOS and some Linux systems, the DocumentFormat.OpenXml library creates ZIP archives
    /// with invalid file permissions (000000 octal) and dates set to Unix epoch (1980-01-01),
    /// preventing Word from opening the file. This method repacks the ZIP archive with proper
    /// DOS-compatible attributes for Word compatibility.
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

            // Recreate with explicit DOS attributes
            using (FileStream zipStream = new FileStream(docxPath, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create, false))
            {
                // Get all files from temp directory
                string[] files = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories);

                foreach (string file in files)
                {
                    // Get relative path within the archive
                    string entryName = Path.GetRelativePath(tempDir, file).Replace('\\', '/');

                    // Read file info for attributes
                    FileInfo fileInfo = new FileInfo(file);

                    // Create entry
                    ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                    // Set proper timestamp
                    entry.LastWriteTime = DateTimeOffset.Now;

                    // Set DOS-compatible external attributes using reflection
                    // External attributes format:
                    // High word (16-31): Unix/DOS specific
                    // Low word (0-15): MS-DOS attributes
                    const uint DOS_FILE_ATTRIBUTE_ARCHIVE = 0x20; // Standard file attribute
                    SetExternalAttributes(entry, DOS_FILE_ATTRIBUTE_ARCHIVE);

                    // Write file contents
                    using (Stream entryStream = entry.Open())
                    using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        fileStream.CopyTo(entryStream);
                    }
                }
            }

            Console.WriteLine("ZIP permissions fixed with DOS-compatible attributes");
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

    /// <summary>
    /// Sets the external attributes on a ZIP entry to ensure DOS/Windows compatibility.
    /// </summary>
    private static void SetExternalAttributes(ZipArchiveEntry entry, uint attributes)
    {
        // Use reflection to set the internal ExternalAttributes property
        // This is necessary because the property is not publicly accessible
        Type entryType = entry.GetType();
        System.Reflection.PropertyInfo? externalAttributesProperty = entryType.GetProperty(
            "ExternalAttributes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (externalAttributesProperty != null && externalAttributesProperty.CanWrite)
        {
            // Set DOS-compatible attributes (0x20 = archive bit for normal files)
            // The format is: high word = OS-specific, low word = MS-DOS attributes
            externalAttributesProperty.SetValue(entry, (int)attributes);
        }
    }

    /// <summary>
    /// Fix TableLook elements to use the correct schema for modern Word versions.
    /// Since old attributes can't be removed through the API, we remove the entire TableLook element.
    /// TableLook is optional and only affects table styling preferences.
    /// </summary>
    private static void FixTableLookElements(WordprocessingDocument document)
    {
        if (document.MainDocumentPart?.Document?.Body == null)
        {
            return;
        }

        // Find all TableLook elements in the document
        var tableLooks = document.MainDocumentPart.Document.Descendants<TableLook>().ToList();
        int removedCount = 0;

        foreach (var tableLook in tableLooks)
        {
            // Remove the TableLook element entirely to avoid schema validation errors
            // TableLook is optional - its absence doesn't affect document functionality
            tableLook.Remove();
            removedCount++;
        }

        if (removedCount > 0)
        {
            Console.WriteLine($"Removed {removedCount} TableLook element(s) for schema compatibility");
        }
    }
}
