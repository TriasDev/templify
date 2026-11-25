// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Testing;

/// <summary>
/// Verifies template definitions the way the builder's own tests do. A consumer builds at runtime, so its CI is where
/// its definitions get verified: call <see cref="VerifyAll"/> from any test framework.
/// </summary>
/// <example>
/// <code>
/// [Fact]
/// public void InvoiceTemplate_IsValid() => TemplateVerifier.VerifyAll(
///     () => new InvoiceTemplate(theme),
///     new TemplateVerificationOptions
///     {
///         Languages = [CultureInfo.GetCultureInfo("de-DE"), CultureInfo.GetCultureInfo("en-US")],
///         Texts = language => catalogs[language.Name],
///         SampleData = [File.ReadAllText("invoice-sample.json")],
///     });
/// </code>
/// </example>
public static class TemplateVerifier
{
    private const string OdtMediaType = "application/vnd.oasis.opendocument.text";
    private static readonly XNamespace _manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    /// <summary>
    /// Builds the definition for every language and format and runs every check; throws when one fails.
    /// </summary>
    /// <param name="definitionFactory">Creates the definition; called once per build, so state cannot leak between builds.</param>
    /// <param name="options">The languages, formats, texts and sample data; null for the defaults.</param>
    /// <returns>The successful report, with the built templates.</returns>
    /// <exception cref="TemplateVerificationException">A check failed; the message lists every failure.</exception>
    public static TemplateVerificationReport VerifyAll(Func<ITemplateDefinition> definitionFactory, TemplateVerificationOptions? options = null)
    {
        TemplateVerificationReport report = Verify(definitionFactory, options);
        return report.IsSuccess ? report : throw new TemplateVerificationException(report);
    }

    /// <summary>
    /// Builds the definition for every language and format and runs every check, collecting the failures:
    /// the build; <c>ValidateTemplate</c> (no errors); the contract (filling with the contract's witness reports no
    /// missing variable and no warning); fills with the sample data; the DOCX schema or ODT package of the template
    /// and of every filled document; byte determinism (also under <see cref="TemplateVerificationOptions.DeterminismCultures"/>);
    /// equal contracts across languages and formats.
    /// </summary>
    /// <param name="definitionFactory">Creates the definition; called once per build.</param>
    /// <param name="options">The languages, formats, texts and sample data; null for the defaults.</param>
    /// <returns>The report.</returns>
    public static TemplateVerificationReport Verify(Func<ITemplateDefinition> definitionFactory, TemplateVerificationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(definitionFactory);
        options ??= new TemplateVerificationOptions();
        if (options.Languages.Count == 0 || options.Formats.Count == 0)
        {
            throw new ArgumentException("At least one language and one format are needed.", nameof(options));
        }

        List<VerifiedTemplate> templates = new List<VerifiedTemplate>();
        List<TemplateVerificationFailure> failures = new List<TemplateVerificationFailure>();

        foreach (CultureInfo language in options.Languages)
        {
            foreach (TemplateFormat format in options.Formats)
            {
                void Fail(VerificationCheck check, string message) =>
                    failures.Add(new TemplateVerificationFailure(language, format, check, message));

                TemplateBuildOptions buildOptions = new TemplateBuildOptions
                {
                    Format = format,
                    Language = language,
                    Texts = options.Texts?.Invoke(language) ?? TextCatalog.Empty,
                };

                GeneratedTemplate template;
                try
                {
                    template = TemplateBuilder.Build(definitionFactory(), buildOptions);
                }
                catch (TemplateBuildException exception)
                {
                    foreach (BuildDiagnostic error in exception.Errors)
                    {
                        Fail(VerificationCheck.Build, error.Code + " " + error.Message);
                    }

                    continue;
                }

                templates.Add(new VerifiedTemplate(language, format, template));
                if (options.FailOnBuildWarnings)
                {
                    foreach (BuildDiagnostic warning in template.Warnings)
                    {
                        Fail(VerificationCheck.BuildWarning, warning.Code + " " + warning.Message);
                    }
                }

                foreach ((VerificationCheck check, string message) in CheckTemplateCore(template))
                {
                    Fail(check, message);
                }

                for (int i = 0; i < options.SampleData.Count; i++)
                {
                    foreach (string problem in CheckFill(template, options.SampleData[i], language))
                    {
                        Fail(VerificationCheck.Fill, $"sample data #{(i + 1).ToString(CultureInfo.InvariantCulture)}: {problem}");
                    }
                }

                foreach (CultureInfo culture in options.DeterminismCultures)
                {
                    string? problem = CheckDeterminism(definitionFactory, buildOptions, template, culture);
                    if (problem != null)
                    {
                        Fail(VerificationCheck.Determinism, problem);
                    }
                }
            }
        }

        if (options.RequireEqualContracts && templates.Count > 1)
        {
            VerifiedTemplate first = templates[0];
            foreach (VerifiedTemplate other in templates.Skip(1).Where(t => t.Template.Contract.Hash != first.Template.Contract.Hash))
            {
                failures.Add(new TemplateVerificationFailure(
                    null,
                    null,
                    VerificationCheck.ContractParity,
                    $"{Describe(other)} reads different data than {Describe(first)}: {ContractDifference(first.Template.Contract, other.Template.Contract)}"));
            }
        }

        return new TemplateVerificationReport(templates, failures);
    }

    /// <summary>
    /// Checks one built template: <c>ValidateTemplate</c> reports no errors, the contract's witness fills it without
    /// missing variables or warnings, and the template and the filled witness pass the package check.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>The problems; empty when the template is fine.</returns>
    public static IReadOnlyList<string> CheckTemplate(GeneratedTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return CheckTemplateCore(template).Select(p => p.Check + ": " + p.Message).ToList();
    }

    /// <summary>
    /// Fills a template with JSON data, failing on missing variables, and checks the result: success, no warnings,
    /// a valid package.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="json">The data.</param>
    /// <param name="culture">The fill culture (numbers, dates); null for the invariant culture.</param>
    /// <returns>The problems; empty when the fill is fine.</returns>
    public static IReadOnlyList<string> CheckFill(GeneratedTemplate template, string json, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(json);
        return Fill(template, culture, (processor, input, output) => processor.ProcessTemplate(input, output, json));
    }

    /// <summary>
    /// Checks a document package: DOCX against the OOXML schema (<c>OpenXmlValidator</c>); ODT for its package
    /// structure (<c>mimetype</c> first, stored, without an extra field; manifest entries present; well-formed XML).
    /// Works for templates and for filled documents.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <param name="package">The package bytes.</param>
    /// <returns>The problems; empty when the package is fine.</returns>
    public static IReadOnlyList<string> CheckPackage(TemplateFormat format, ReadOnlyMemory<byte> package)
    {
        try
        {
            return format switch
            {
                TemplateFormat.Docx => CheckDocx(package),
                TemplateFormat.Odt => CheckOdt(package.Span),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Only DOCX and ODT packages can be checked."),
            };
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or OpenXmlPackageException or IOException or FormatException)
        {
            return [$"The package cannot be read: {exception.Message}"];
        }
    }

    private static List<(VerificationCheck Check, string Message)> CheckTemplateCore(GeneratedTemplate template)
    {
        List<(VerificationCheck, string)> problems = new List<(VerificationCheck, string)>();
        TemplateProcessor processor = new TemplateProcessor();

        using (Stream stream = template.OpenRead())
        {
            ValidationResult validation = processor.ValidateTemplate(stream);
            problems.AddRange(validation.Errors.Select(e => (VerificationCheck.Structure, $"{e.Type}: {e.Message}")));
        }

        IReadOnlyDictionary<string, object?> witness = template.Contract.CreateWitness();
        using (Stream stream = template.OpenRead())
        {
            ValidationResult validation = processor.ValidateTemplate(stream, witness);
            problems.AddRange(validation.MissingVariables.Select(v => (VerificationCheck.Contract, $"'{v}' is read but not in the data contract.")));
        }

        problems.AddRange(Fill(template, null, (p, input, output) => p.ProcessTemplate(input, output, witness))
            .Select(p => (VerificationCheck.Contract, "witness fill: " + p)));
        problems.AddRange(CheckPackage(template.Format, template.Content).Select(p => (VerificationCheck.Package, "template: " + p)));
        return problems;
    }

    private static List<string> Fill(GeneratedTemplate template, CultureInfo? culture, Func<TemplateProcessor, Stream, Stream, ProcessingResult> fill)
    {
        TemplateProcessor processor = new TemplateProcessor(new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException,
            Culture = culture ?? CultureInfo.InvariantCulture,
        });

        using Stream input = template.OpenRead();
        using MemoryStream output = new MemoryStream();
        ProcessingResult result;
        try
        {
            result = fill(processor, input, output);
        }
        catch (InvalidOperationException exception)
        {
            // MissingVariableBehavior.ThrowException.
            return [exception.Message];
        }

        if (!result.IsSuccess)
        {
            return [result.ErrorMessage ?? "Processing failed."];
        }

        List<string> problems = result.Warnings.Select(w => $"warning {w.Type}: {w.Message}").ToList();
        problems.AddRange(CheckPackage(template.Format, output.ToArray()).Select(p => "filled document: " + p));
        return problems;
    }

    private static string? CheckDeterminism(Func<ITemplateDefinition> factory, TemplateBuildOptions options, GeneratedTemplate first, CultureInfo culture)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            GeneratedTemplate again = TemplateBuilder.Build(factory(), options);
            return again.ContentHash == first.ContentHash
                ? null
                : $"a second build under the current culture {culture.Name} gave different bytes{FirstDifference(first.Content.Span, again.Content.Span)}.";
        }
        catch (TemplateBuildException exception)
        {
            return $"a second build under the current culture {culture.Name} failed: {exception.Message}";
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    private static string FirstDifference(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        int length = Math.Min(left.Length, right.Length);
        int index = left[..length].CommonPrefixLength(right[..length]);
        return $" (first difference at byte {index.ToString(CultureInfo.InvariantCulture)}; a definition must not depend on the current culture, the clock or random values)";
    }

    private static string Describe(VerifiedTemplate template) =>
        template.Language.Name + "/" + template.Format.ToString().ToLowerInvariant();

    private static string ContractDifference(DataContract left, DataContract right)
    {
        HashSet<ContractEntry> a = left.Entries.ToHashSet();
        HashSet<ContractEntry> b = right.Entries.ToHashSet();
        IEnumerable<string> missing = a.Except(b).Select(e => "-" + e.Path + " (" + e.Usage + ")");
        IEnumerable<string> extra = b.Except(a).Select(e => "+" + e.Path + " (" + e.Usage + ")");
        return string.Join(", ", missing.Concat(extra));
    }

    private static List<string> CheckDocx(ReadOnlyMemory<byte> package)
    {
        using MemoryStream stream = new MemoryStream(package.ToArray(), writable: false);
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, false);
        return new OpenXmlValidator()
            .Validate(document)
            .Select(e => $"{e.Part?.Uri} {e.Path?.XPath}: {e.Description}")
            .ToList();
    }

    private static List<string> CheckOdt(ReadOnlySpan<byte> package)
    {
        List<string> problems = new List<string>();

        // The mimetype entry: first, stored, no data descriptor, no extra field, the ODT media type (ODF 1.3 part 2, 3.3).
        const int header = 30;
        if (package.Length < header + 8 + OdtMediaType.Length || BinaryPrimitives.ReadUInt32LittleEndian(package) != 0x04034b50u)
        {
            return ["The package is not a ZIP file."];
        }

        if ((BinaryPrimitives.ReadUInt16LittleEndian(package[6..]) & 0x08) != 0)
        {
            problems.Add("The mimetype entry has a data descriptor.");
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(package[8..]) != 0)
        {
            problems.Add("The mimetype entry is compressed; it must be stored.");
        }

        ushort nameLength = BinaryPrimitives.ReadUInt16LittleEndian(package[26..]);
        ushort extraLength = BinaryPrimitives.ReadUInt16LittleEndian(package[28..]);
        if (nameLength != 8 || Encoding.ASCII.GetString(package.Slice(header, 8)) != "mimetype")
        {
            problems.Add("The first entry is not mimetype.");
            return problems;
        }

        if (extraLength != 0)
        {
            problems.Add("The mimetype entry has an extra field.");
        }

        if (Encoding.ASCII.GetString(package.Slice(header + 8 + extraLength, OdtMediaType.Length)) != OdtMediaType)
        {
            problems.Add($"The mimetype is not {OdtMediaType}.");
        }

        using MemoryStream stream = new MemoryStream(package.ToArray(), writable: false);
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
        HashSet<string> names = archive.Entries.Select(e => e.FullName).ToHashSet(StringComparer.Ordinal);
        ZipArchiveEntry? manifestEntry = archive.GetEntry("META-INF/manifest.xml");
        if (manifestEntry is null)
        {
            problems.Add("META-INF/manifest.xml is missing.");
        }
        else
        {
            XDocument manifest;
            using (Stream manifestStream = manifestEntry.Open())
            {
                manifest = XDocument.Load(manifestStream);
            }

            foreach (XElement entry in manifest.Root?.Elements(_manifest + "file-entry") ?? [])
            {
                string path = (string?)entry.Attribute(_manifest + "full-path") ?? string.Empty;
                if (path == "/")
                {
                    if ((string?)entry.Attribute(_manifest + "media-type") != OdtMediaType)
                    {
                        problems.Add("The manifest's root entry does not have the ODT media type.");
                    }
                }
                else if (!path.EndsWith('/') && !names.Contains(path))
                {
                    problems.Add($"The manifest lists '{path}', which is not in the package.");
                }
            }
        }

        foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.Ordinal)))
        {
            try
            {
                using Stream entryStream = entry.Open();
                XDocument.Load(entryStream);
            }
            catch (XmlException exception)
            {
                problems.Add($"{entry.FullName} is not well-formed XML: {exception.Message}");
            }
        }

        return problems;
    }
}
