// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Helpers;

/// <summary>
/// Runs the build → stream → process → verify sequence used by integration tests.
/// </summary>
/// <remarks>
/// Without explicit options the processor runs with <see cref="CultureInfo.InvariantCulture"/>, so assertions on
/// numbers and dates do not depend on the machine culture (CI runs the suite under de-DE, tr-TR and ar-SA).
/// Tests that pass their own options are responsible for the culture they set.
/// </remarks>
public static class TemplateTestHarness
{
    /// <summary>
    /// Creates the default options used by the harness: <see cref="CultureInfo.InvariantCulture"/>, everything else default.
    /// </summary>
    public static PlaceholderReplacementOptions InvariantOptions() =>
        new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture };

    /// <summary>
    /// Creates a processor with <see cref="InvariantOptions"/> (boolean formatters follow the culture, so they are invariant too).
    /// </summary>
    public static DocumentTemplateProcessor CreateInvariantProcessor() =>
        new DocumentTemplateProcessor(InvariantOptions());

    /// <summary>
    /// Builds the template from <paramref name="builder"/> and processes it with <paramref name="data"/>.
    /// </summary>
    public static TemplateTestRun Process(
        DocumentBuilder builder,
        Dictionary<string, object> data,
        PlaceholderReplacementOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return Process(builder.ToStream(), data, options);
    }

    /// <summary>
    /// Processes an already built template stream with <paramref name="data"/>.
    /// </summary>
    public static TemplateTestRun Process(
        Stream template,
        Dictionary<string, object> data,
        PlaceholderReplacementOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options ?? InvariantOptions());
        MemoryStream output = new MemoryStream();
        ProcessingResult result = processor.ProcessTemplate(template, output, data);
        return new TemplateTestRun(result, output);
    }

    /// <summary>
    /// Builds the template from <paramref name="builder"/> and processes it with JSON data.
    /// </summary>
    public static TemplateTestRun ProcessJson(
        DocumentBuilder builder,
        string jsonData,
        PlaceholderReplacementOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options ?? InvariantOptions());
        MemoryStream output = new MemoryStream();
        ProcessingResult result = processor.ProcessTemplate(builder.ToStream(), output, jsonData);
        return new TemplateTestRun(result, output);
    }
}

/// <summary>
/// The outcome of a <see cref="TemplateTestHarness"/> run: the processing result and the output document.
/// </summary>
public sealed class TemplateTestRun : IDisposable
{
    private DocumentVerifier? _verifier;

    internal TemplateTestRun(ProcessingResult result, MemoryStream output)
    {
        Result = result;
        Output = output;
    }

    /// <summary>
    /// The processing result.
    /// </summary>
    public ProcessingResult Result { get; }

    /// <summary>
    /// The processed document stream.
    /// </summary>
    public MemoryStream Output { get; }

    /// <summary>
    /// A verifier over the output document, created on first access.
    /// Asserts that processing succeeded first, so a failed run reports its error message instead of an unreadable document.
    /// </summary>
    public DocumentVerifier Verifier
    {
        get
        {
            if (_verifier == null)
            {
                Assert.True(Result.IsSuccess, $"Processing failed: {Result.ErrorMessage}");
                _verifier = new DocumentVerifier(Output);
            }

            return _verifier;
        }
    }

    /// <summary>
    /// Disposes the verifier and the output stream.
    /// </summary>
    public void Dispose()
    {
        _verifier?.Dispose();
        Output.Dispose();
    }
}
