// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Placeholders;

/// <summary>
/// Handles placeholder replacement in table cells.
/// </summary>
internal sealed class TableReplacer
{
    private readonly DocumentBodyReplacer _bodyReplacer;

    public TableReplacer(DocumentBodyReplacer bodyReplacer)
    {
        _bodyReplacer = bodyReplacer ?? throw new ArgumentNullException(nameof(bodyReplacer));
    }

    /// <summary>
    /// Replaces placeholders in all tables in the document.
    /// </summary>
    /// <param name="document">The Word document to process.</param>
    /// <param name="data">The data dictionary containing replacement values.</param>
    /// <param name="options">Processing options.</param>
    /// <param name="missingVariables">Collection to track missing variables.</param>
    /// <returns>The number of replacements made.</returns>
    public int ReplaceInTables(
        WordprocessingDocument document,
        Dictionary<string, object> data,
        PlaceholderReplacementOptions options,
        HashSet<string> missingVariables)
    {
        if (document.MainDocumentPart?.Document?.Body == null)
        {
            return 0;
        }

        int replacementCount = 0;
        Body body = document.MainDocumentPart.Document.Body;

        // Process all tables in the body
        IEnumerable<Table> tables = body.Descendants<Table>();

        foreach (Table table in tables)
        {
            replacementCount += ProcessTable(table, data, options, missingVariables);
        }

        return replacementCount;
    }

    /// <summary>
    /// Processes a single table, replacing placeholders in all cells.
    /// </summary>
    private int ProcessTable(
        Table table,
        Dictionary<string, object> data,
        PlaceholderReplacementOptions options,
        HashSet<string> missingVariables)
    {
        int replacementCount = 0;

        // Get all table cells
        IEnumerable<TableCell> cells = table.Descendants<TableCell>();

        foreach (TableCell cell in cells)
        {
            // Process each paragraph in the cell
            IEnumerable<Paragraph> paragraphs = cell.Descendants<Paragraph>();

            foreach (Paragraph paragraph in paragraphs)
            {
                replacementCount += _bodyReplacer.ProcessParagraph(paragraph, data, options, missingVariables);
            }
        }

        return replacementCount;
    }
}
