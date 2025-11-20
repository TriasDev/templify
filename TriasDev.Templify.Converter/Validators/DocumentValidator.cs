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
    /// <summary>
    /// Validate a Word document.
    /// </summary>
    /// <param name="documentPath">Path to the document to validate.</param>
    /// <returns>True if the document is valid.</returns>
    public bool ValidateDocument(string documentPath)
    {
        if (!File.Exists(documentPath))
        {
            Console.WriteLine($"ERROR: File not found: {documentPath}");
            return false;
        }

        try
        {
            using WordprocessingDocument document = WordprocessingDocument.Open(documentPath, false);

            Console.WriteLine($"Validating document: {documentPath}");
            Console.WriteLine();

            // Check basic structure
            if (document.MainDocumentPart == null)
            {
                Console.WriteLine("ERROR: Document has no main document part");
                return false;
            }

            if (document.MainDocumentPart.Document == null)
            {
                Console.WriteLine("ERROR: Document has no body");
                return false;
            }

            Console.WriteLine("✓ Document structure is valid");
            Console.WriteLine();

            // Validate using OpenXML SDK validator
            OpenXmlValidator validator = new OpenXmlValidator();
            List<ValidationErrorInfo> errors = validator.Validate(document).ToList();

            if (errors.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Document is valid! No errors found.");
                Console.ResetColor();
                return true;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Found {errors.Count} validation errors:");
                Console.ResetColor();
                Console.WriteLine();

                int displayCount = Math.Min(errors.Count, 20);
                for (int i = 0; i < displayCount; i++)
                {
                    ValidationErrorInfo error = errors[i];
                    Console.WriteLine($"Error {i + 1}:");
                    Console.WriteLine($"  Type: {error.ErrorType}");
                    Console.WriteLine($"  Description: {error.Description}");
                    if (error.Node != null)
                    {
                        Console.WriteLine($"  Location: {error.Path?.XPath}");
                    }
                    Console.WriteLine();
                }

                if (errors.Count > 20)
                {
                    Console.WriteLine($"... and {errors.Count - 20} more errors");
                }

                return false;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: Failed to open document: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }
}
