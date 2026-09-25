// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace TriasDev.Templify.Converter.Validators;

/// <summary>
/// Validates that a converted document is valid and can be opened.
/// </summary>
public class DocumentValidator
{
    private readonly TextWriter _out;
    private readonly TextWriter _error;

    /// <summary>
    /// Create a validator that reports to the console (errors to stderr).
    /// </summary>
    public DocumentValidator()
        : this(Console.Out, Console.Error)
    {
    }

    /// <summary>
    /// Create a validator that reports to the given writers.
    /// </summary>
    public DocumentValidator(TextWriter output, TextWriter error)
    {
        _out = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    /// <summary>
    /// Validate a Word document.
    /// </summary>
    /// <param name="documentPath">Path to the document to validate.</param>
    /// <returns>True if the document is valid.</returns>
    public bool ValidateDocument(string documentPath)
    {
        if (!File.Exists(documentPath))
        {
            _error.WriteLine($"ERROR: File not found: {documentPath}");
            return false;
        }

        WordprocessingDocument document;
        try
        {
            document = WordprocessingDocument.Open(documentPath, false);
        }
        catch (Exception ex)
        {
            _error.WriteLine($"ERROR: Failed to open document: {ex.Message}");
            return false;
        }

        using (document)
        {
            _out.WriteLine($"Validating document: {documentPath}");
            _out.WriteLine();

            if (document.MainDocumentPart == null)
            {
                _error.WriteLine("ERROR: Document has no main document part");
                return false;
            }

            if (document.MainDocumentPart.Document == null)
            {
                _error.WriteLine("ERROR: Main document part has no document element");
                return false;
            }

            if (document.MainDocumentPart.Document.Body == null)
            {
                _error.WriteLine("ERROR: Document has no body");
                return false;
            }

            _out.WriteLine("✓ Document structure is valid");
            _out.WriteLine();

            List<ValidationErrorInfo> errors = new OpenXmlValidator().Validate(document).ToList();
            if (errors.Count == 0)
            {
                _out.WriteLine("✓ Document is valid! No errors found.");
                return true;
            }

            _error.WriteLine($"✗ Found {errors.Count} validation errors:");
            _error.WriteLine();

            int displayCount = Math.Min(errors.Count, 20);
            for (int i = 0; i < displayCount; i++)
            {
                ValidationErrorInfo error = errors[i];
                _error.WriteLine($"Error {i + 1}:");
                _error.WriteLine($"  Type: {error.ErrorType}");
                _error.WriteLine($"  Description: {error.Description}");
                if (error.Path?.XPath != null)
                {
                    _error.WriteLine($"  Location: {error.Path.XPath}");
                }

                _error.WriteLine();
            }

            if (errors.Count > 20)
            {
                _error.WriteLine($"... and {errors.Count - 20} more errors");
            }

            return false;
        }
    }
}
