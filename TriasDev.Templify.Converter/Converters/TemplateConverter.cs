// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Converter.Models;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Options for <see cref="TemplateConverter"/>.
/// </summary>
public sealed class ConversionOptions
{
    /// <summary>
    /// When true, every content control that is left after the conversion (table of contents,
    /// citations, check boxes, unknown tags, ...) is unwrapped as well. Default: false — only
    /// OpenXMLTemplates controls are converted/unwrapped and all other controls are kept.
    /// </summary>
    public bool UnwrapAllControls { get; init; }
}

/// <summary>
/// Main converter that orchestrates the conversion from OpenXMLTemplates to Templify.
/// </summary>
public class TemplateConverter
{
    private readonly VariableConverter _variableConverter = new();
    private readonly ConditionalConverter _conditionalConverter = new();
    private readonly RepeatingConverter _repeatingConverter = new();
    private readonly ConversionOptions _options;

    /// <summary>
    /// Create a converter with default options.
    /// </summary>
    public TemplateConverter()
        : this(new ConversionOptions())
    {
    }

    /// <summary>
    /// Create a converter with the given options.
    /// </summary>
    public TemplateConverter(ConversionOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Convert a template from OpenXMLTemplates to Templify.
    /// </summary>
    /// <param name="inputPath">Path to the original template. It is never modified directly.</param>
    /// <param name="outputPath">Path where the converted template will be saved.</param>
    /// <returns>Conversion result with statistics and any issues.</returns>
    public ConversionResult ConvertTemplate(string inputPath, string outputPath)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Template file not found: {inputPath}", inputPath);
        }

        ConversionResult result = SafeFileWriter.Write(inputPath, outputPath, workingPath =>
        {
            ConversionResult conversion = new ConversionResult();
            using (WordprocessingDocument document = WordprocessingDocument.Open(workingPath, true))
            {
                ConvertDocument(document, conversion);
            }

            ValidateWithTemplify(workingPath, conversion);
            return conversion;
        });

        return result;
    }

    /// <summary>
    /// Convert an already opened (editable) document in place.
    /// </summary>
    public void ConvertDocument(WordprocessingDocument document, ConversionResult result)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(result);

        if (document.MainDocumentPart?.Document?.Body == null)
        {
            throw new InvalidOperationException("Document has no body");
        }

        foreach (OpenXmlPartRootElement root in OpenXmlHelpers.GetContentRoots(document))
        {
            ConvertPart(root, result);

            if (_options.UnwrapAllControls)
            {
                int cleaned = 0;
                foreach (SdtElement sdt in root.Descendants<SdtElement>().ToList())
                {
                    if (sdt.Parent != null)
                    {
                        OpenXmlHelpers.UnwrapContentControl(sdt);
                        cleaned++;
                    }
                }

                result.CleanedSdtElements += cleaned;
            }

            // Old TableLook attributes fail schema validation in current Word versions;
            // TableLook is optional and only affects table style preferences.
            foreach (TableLook tableLook in root.Descendants<TableLook>().ToList())
            {
                tableLook.Remove();
            }

            root.Save();
        }
    }

    private void ConvertPart(OpenXmlPartRootElement root, ConversionResult result)
    {
        // Deepest controls first, so inner controls are converted before their containers are unwrapped.
        List<SdtElement> controls = root.Descendants<SdtElement>()
            .Where(sdt => OpenXmlTemplatesTag.IsOpenXmlTemplatesTag(OpenXmlHelpers.GetContentControlTag(sdt)))
            .OrderByDescending(GetDepth)
            .ToList();

        List<SdtElement> otherControls = root.Descendants<SdtElement>()
            .Where(sdt => !OpenXmlTemplatesTag.IsOpenXmlTemplatesTag(OpenXmlHelpers.GetContentControlTag(sdt)))
            .ToList();

        result.TotalControls += controls.Count;
        foreach (SdtElement other in otherControls)
        {
            string? tag = OpenXmlHelpers.GetContentControlTag(other);
            if (tag != null && !_options.UnwrapAllControls)
            {
                result.Warnings.Add($"{OpenXmlHelpers.GetPartName(root)}: content control '{tag}' is not an OpenXMLTemplates control and was kept");
            }
        }

        foreach (SdtElement sdt in controls)
        {
            string tagValue = OpenXmlHelpers.GetContentControlTag(sdt)!;
            OpenXmlTemplatesTag tag = OpenXmlTemplatesTag.Parse(tagValue);
            string location = OpenXmlHelpers.GetPartName(root);

            try
            {
                List<string> warnings = new();
                switch (tag.Type)
                {
                    case ControlType.Variable:
                        if (string.Equals(tag.VariablePath, "index", StringComparison.OrdinalIgnoreCase)
                            && HasRepeatingAncestor(sdt))
                        {
                            warnings.Add($"{tagValue}: OpenXMLTemplates 'index' is the 1-based item number; Templify provides the 0-based {{{{@index}}}} — review");
                        }

                        _variableConverter.Convert(sdt, tag);
                        break;
                    case ControlType.Conditional:
                        _conditionalConverter.Convert(sdt, tag, warnings);
                        break;
                    case ControlType.Repeating:
                        _repeatingConverter.Convert(sdt, tag);
                        break;
                }

                result.ConvertedControls++;
                IncrementConversionCount(result, tag.Type);
                foreach (string note in tag.ReviewNotes)
                {
                    result.Warnings.Add($"{location}: {tagValue}: {note}");
                }

                foreach (string warning in warnings)
                {
                    result.Warnings.Add($"{location}: {warning}");
                }
            }
            catch (ControlConversionException ex)
            {
                result.SkippedControls++;
                result.Errors.Add($"{location}: could not convert '{tagValue}' (control kept for manual conversion): {ex.Message}");
                result.FailedConversions.Add(new ControlInfo
                {
                    Tag = tagValue,
                    Type = tag.Type,
                    Location = location,
                    Notes = new List<string> { ex.Message },
                });
            }
        }
    }

    private static bool HasRepeatingAncestor(SdtElement sdt)
    {
        return sdt.Ancestors<SdtElement>().Any(ancestor =>
            OpenXmlTemplatesTag.DetermineType(OpenXmlHelpers.GetContentControlTag(ancestor) ?? string.Empty) == ControlType.Repeating);
    }

    /// <summary>
    /// Let the core library check the converted template: static validation (unbalanced markers,
    /// invalid syntax, ...) plus a dry run with empty data, which catches layouts the installed
    /// Templify version cannot process.
    /// </summary>
    private static void ValidateWithTemplify(string path, ConversionResult result)
    {
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();

        using (FileStream stream = File.OpenRead(path))
        {
            ValidationResult validation = processor.ValidateTemplate(stream);
            foreach (ValidationError error in validation.Errors)
            {
                result.Errors.Add($"Templify validation: {error.Message}");
            }
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            using MemoryStream output = new MemoryStream();
            ProcessingResult processing = processor.ProcessTemplate(stream, output, new Dictionary<string, object>());
            if (!processing.IsSuccess)
            {
                result.Errors.Add($"Templify cannot process the converted template: {processing.ErrorMessage}");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
        {
            result.Errors.Add($"Templify cannot process the converted template: {ex.Message}");
        }
    }

    private static int GetDepth(OpenXmlElement element)
    {
        int depth = 0;
        for (OpenXmlElement? current = element.Parent; current != null; current = current.Parent)
        {
            depth++;
        }

        return depth;
    }

    private static void IncrementConversionCount(ConversionResult result, ControlType type)
    {
        result.ConversionsByType.TryGetValue(type, out int count);
        result.ConversionsByType[type] = count + 1;
    }
}
