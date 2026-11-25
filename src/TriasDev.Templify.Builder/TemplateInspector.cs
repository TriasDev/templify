// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder;

/// <summary>How an inspected template relates to the current definition.</summary>
public enum TemplateOrigin
{
    /// <summary>The template has no stamp: edited in an application that dropped it, or not built by the builder.</summary>
    Unknown = 0,

    /// <summary>The stamp names the expected template and the current data contract.</summary>
    CurrentContract = 1,

    /// <summary>The stamp names the expected template, built against a different data contract (typically an older version).</summary>
    DifferentContract = 2,

    /// <summary>The stamp names a different template than the one expected.</summary>
    OtherTemplate = 3,
}

/// <summary>What <see cref="TemplateInspector.Inspect"/> found in a template.</summary>
/// <param name="Format">The file format; <see cref="TemplateFormat.Unknown"/> for anything else.</param>
/// <param name="Stamp">The builder's stamp; null when there is none.</param>
public sealed record TemplateInspection(TemplateFormat Format, TemplateStamp? Stamp);

/// <summary>The result of <see cref="TemplateInspector.CheckUpload"/>.</summary>
public sealed class UploadCheckResult
{
    internal UploadCheckResult(TemplateFormat format, TemplateStamp? stamp, TemplateOrigin origin, ValidationResult syntax, IReadOnlyList<string> unknownPaths)
    {
        Format = format;
        Stamp = stamp;
        Origin = origin;
        Syntax = syntax;
        UnknownPaths = unknownPaths;
    }

    /// <summary>
    /// Gets a value indicating whether the template can be filled with data of the current contract: a known format,
    /// valid template syntax and no unknown paths.
    /// </summary>
    public bool IsUsable => Format != TemplateFormat.Unknown && Syntax.IsValid && UnknownPaths.Count == 0;

    /// <summary>Gets the file format.</summary>
    public TemplateFormat Format { get; }

    /// <summary>Gets the builder's stamp; null when there is none.</summary>
    public TemplateStamp? Stamp { get; }

    /// <summary>Gets how the template relates to the current definition, by its stamp.</summary>
    public TemplateOrigin Origin { get; }

    /// <summary>Gets the result of <see cref="TemplateProcessor.ValidateTemplate(Stream)"/>: the template's own syntax.</summary>
    public ValidationResult Syntax { get; }

    /// <summary>
    /// Gets the paths the template uses that the current data contract does not provide, as the template writes them
    /// (with its own loop variable names), sorted.
    /// </summary>
    /// <remarks>
    /// Paths used as comparison operands (<c>{{#if order.priority = "high"}}</c>) are found only once the engine reports
    /// them (TriasDev/templify#271).
    /// </remarks>
    public IReadOnlyList<string> UnknownPaths { get; }
}

/// <summary>
/// Reads the builder's stamp from a template and checks an uploaded (possibly hand-edited) template against the
/// current data contract.
/// </summary>
public static partial class TemplateInspector
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
    /// Checks a template a user uploaded: its syntax, its stamp against the expected identity and the current
    /// contract, and the paths it uses that the current data does not provide.
    /// </summary>
    /// <param name="template">The uploaded template; it is read, not changed.</param>
    /// <param name="current">The contract of the current definition (<see cref="GeneratedTemplate.Contract"/>).</param>
    /// <param name="expected">The identity the upload should have; null to skip that check.</param>
    /// <returns>The result.</returns>
    public static UploadCheckResult CheckUpload(Stream template, DataContract current, TemplateIdentity? expected = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(current);
        byte[] bytes = ReadAll(template);
        TemplateFormat format = DetectFormat(bytes);
        TemplateStamp? stamp = ReadStamp(bytes, format);

        TemplateProcessor processor = new TemplateProcessor();
        ValidationResult syntax;
        using (MemoryStream stream = new MemoryStream(bytes, writable: false))
        {
            syntax = processor.ValidateTemplate(stream);
        }

        List<string> unknown = new List<string>();
        if (format != TemplateFormat.Unknown)
        {
            using MemoryStream stream = new MemoryStream(bytes, writable: false);
            ValidationResult withData = processor.ValidateTemplate(stream, current.CreateWitness());
            unknown.AddRange(withData.MissingVariables);
            unknown.AddRange(withData.Warnings
                .Where(w => w.Type == ValidationWarningType.MissingConditionVariable)
                .Select(w => ConditionVariable().Match(w.Message))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value));
        }

        TemplateOrigin origin = stamp is null ? TemplateOrigin.Unknown
            : expected != null && !string.Equals(stamp.TemplateId, expected.Id, StringComparison.Ordinal) ? TemplateOrigin.OtherTemplate
            : string.Equals(stamp.ContractHash, current.Hash, StringComparison.Ordinal) ? TemplateOrigin.CurrentContract
            : TemplateOrigin.DifferentContract;

        return new UploadCheckResult(
            format,
            stamp,
            origin,
            syntax,
            unknown.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList());
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
            // A damaged package has no readable stamp; CheckUpload reports its syntax through the engine.
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

    // Until the engine exposes the variable of a MissingConditionVariable warning (TriasDev/templify#275), its name is
    // read from the message; a test fails if the wording changes.
    [GeneratedRegex(@"uses variable '([^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex ConditionVariable();
}
