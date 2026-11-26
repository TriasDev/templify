// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder;

/// <summary>What <see cref="TemplateInspector.Inspect"/> found in a template.</summary>
/// <param name="Format">The file format; <see cref="TemplateFormat.Unknown"/> for anything else.</param>
/// <param name="Stamp">The builder's stamp; null when there is none.</param>
public sealed record TemplateInspection(TemplateFormat Format, TemplateStamp? Stamp);

/// <summary>
/// Reads the builder's stamp from a template and checks stored templates (copies and JSON definitions) against the
/// current data.
/// </summary>
public static class TemplateInspector
{
    /// <summary>Reads a template's format and stamp.</summary>
    /// <param name="template">The template; it is read, not changed.</param>
    /// <returns>The format and the stamp.</returns>
    public static TemplateInspection Inspect(Stream template)
    {
        ArgumentNullException.ThrowIfNull(template);
        byte[] bytes = ReadAll(template);
        TemplateFormat format = DetectFormat(bytes);
        return new TemplateInspection(format, ReadStamp(bytes, format));
    }

    /// <summary>
    /// Checks a stored template (a DOCX or ODT copy, typically edited by a user) against the current data: its syntax,
    /// every path it reads (against the catalogue, so a copy may read fields the standard template does not), a trial
    /// fill, and its stamp (identity, version, component versions).
    /// </summary>
    /// <param name="template">The template; it is read, not changed.</param>
    /// <param name="options">The current data catalogue, and the identity and components to compare the stamp with.</param>
    /// <returns>The status and the problems.</returns>
    public static TemplateCheckResult Check(Stream template, TemplateCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(options);
        byte[] bytes = ReadAll(template);
        TemplateFormat format = DetectFormat(bytes);
        TemplateStamp? stamp = ReadStamp(bytes, format);
        List<TemplateCheckProblem> problems = new List<TemplateCheckProblem>();

        if (format == TemplateFormat.Unknown)
        {
            problems.Add(new TemplateCheckProblem(CheckCodes.Syntax, TemplateCheckSeverity.Error, "The file is not a DOCX or ODT template."));
            return new TemplateCheckResult(format, stamp, problems);
        }

        ValidationResult validation = options.Data.ValidateTemplate(bytes, out IReadOnlyCollection<string> openNames);
        foreach (ValidationError error in validation.Errors.Where(e => e.Type != ValidationErrorType.MissingVariable))
        {
            problems.Add(new TemplateCheckProblem(CheckCodes.Syntax, TemplateCheckSeverity.Error, error.Message) { Location = error.Location });
        }

        foreach (string path in validation.MissingVariables.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            problems.Add(new TemplateCheckProblem(CheckCodes.UnknownPath, TemplateCheckSeverity.Error, $"The data has no '{path}'.") { DataPath = path });
        }

        foreach (ValidationWarning warning in validation.Warnings.Where(w => w.Type == ValidationWarningType.MissingConditionVariable))
        {
            foreach (string path in DataCatalog.MissingNames(new ValidationResult { Warnings = [warning] }))
            {
                problems.Add(new TemplateCheckProblem(CheckCodes.UnknownPath, TemplateCheckSeverity.Error, $"A condition reads '{path}', which the data does not have.") { DataPath = path, Location = warning.Location });
            }
        }

        if (problems.Count == 0)
        {
            TrialFill(bytes, options.Data.CreateWitness(openNames), problems);
        }

        JudgeStamp(stamp, options, problems);
        return new TemplateCheckResult(format, stamp, problems);
    }

    /// <summary>Fills the template with witness data: what validation does not see (a loop over a value) fails here.</summary>
    private static void TrialFill(byte[] bytes, IReadOnlyDictionary<string, object?> witness, List<TemplateCheckProblem> problems)
    {
        using MemoryStream input = new MemoryStream(bytes, writable: false);
        using MemoryStream output = new MemoryStream();
        ProcessingResult result = new TemplateProcessor().ProcessTemplate(input, output, witness);
        if (!result.IsSuccess)
        {
            problems.Add(new TemplateCheckProblem(CheckCodes.WrongKind, TemplateCheckSeverity.Error, result.ErrorMessage ?? "Filling the template failed."));
        }
    }

    private static void JudgeStamp(TemplateStamp? stamp, TemplateCheckOptions options, List<TemplateCheckProblem> problems)
    {
        if (stamp is null)
        {
            problems.Add(new TemplateCheckProblem(CheckCodes.NoStamp, TemplateCheckSeverity.Warning, "The template has no builder stamp: its origin is unknown (edited in an application that dropped it, or not built by the builder)."));
            return;
        }

        if (options.Expected is { } expected)
        {
            if (!string.Equals(stamp.TemplateId, expected.Id, StringComparison.Ordinal))
            {
                problems.Add(new TemplateCheckProblem(CheckCodes.OtherTemplate, TemplateCheckSeverity.Warning, $"The template comes from '{stamp.TemplateId}', not from '{expected.Id}'."));
            }
            else if (stamp.Version < expected.Version)
            {
                problems.Add(new TemplateCheckProblem(CheckCodes.OlderVersion, TemplateCheckSeverity.Warning, $"The template comes from version {stamp.Version} of '{expected.Id}'; the current version is {expected.Version}."));
            }
        }

        if (options.Components is { } components)
        {
            IReadOnlyDictionary<string, int> current = components.Versions();
            foreach ((string name, int version) in stamp.Components)
            {
                if (!current.TryGetValue(name, out int now))
                {
                    problems.Add(new TemplateCheckProblem(CheckCodes.OlderComponent, TemplateCheckSeverity.Warning, $"The template was built with the component {name} (version {version}), which is no longer registered."));
                }
                else if (version < now)
                {
                    problems.Add(new TemplateCheckProblem(CheckCodes.OlderComponent, TemplateCheckSeverity.Warning, $"The template was built with version {version} of the component {name}; the current version is {now}."));
                }
            }
        }
    }

    /// <summary>The builder's document properties of a package (name → value).</summary>
    internal static Dictionary<string, string> ReadProperties(byte[] bytes, TemplateFormat format)
    {
        Dictionary<string, string> properties = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            if (format == TemplateFormat.Docx)
            {
                using MemoryStream stream = new MemoryStream(bytes, writable: false);
                using WordprocessingDocument document = WordprocessingDocument.Open(stream, false);
                foreach (CustomDocumentProperty property in document.CustomFilePropertiesPart?.Properties?.Elements<CustomDocumentProperty>() ?? [])
                {
                    if (property.Name?.Value is { } name && name.StartsWith(TemplateStamp.PropertyPrefix, StringComparison.Ordinal))
                    {
                        properties[name] = property.InnerText;
                    }
                }
            }
            else if (format == TemplateFormat.Odt)
            {
                XNamespace meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
                using MemoryStream stream = new MemoryStream(bytes, writable: false);
                using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
                if (archive.GetEntry("meta.xml") is { } entry)
                {
                    using Stream metaStream = entry.Open();
                    foreach (XElement property in XDocument.Load(metaStream).Descendants(meta + "user-defined"))
                    {
                        if ((string?)property.Attribute(meta + "name") is { } name && name.StartsWith(TemplateStamp.PropertyPrefix, StringComparison.Ordinal))
                        {
                            properties[name] = property.Value;
                        }
                    }
                }
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Xml.XmlException or OpenXmlPackageException or IOException)
        {
            // A damaged package has no readable stamp; Check reports its syntax through the engine.
        }

        return properties;
    }

    private static TemplateStamp? ReadStamp(byte[] bytes, TemplateFormat format) =>
        format == TemplateFormat.Unknown ? null : TemplateStamp.FromProperties(ReadProperties(bytes, format));

    private static TemplateFormat DetectFormat(byte[] bytes)
    {
        using MemoryStream stream = new MemoryStream(bytes, writable: false);
        return TemplateProcessor.DetectFormat(stream);
    }

    private static byte[] ReadAll(Stream stream)
    {
        using MemoryStream buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
