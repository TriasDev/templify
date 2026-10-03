// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Demo;

/// <summary>
/// Helpers for building demo Word documents.
/// </summary>
internal partial class Program
{
    // Helper methods for document creation
    private static void AddTitle(Body body, string text)
    {
        Paragraph para = body.AppendChild(new Paragraph());
        Run run = para.AppendChild(new Run());
        RunProperties props = run.AppendChild(new RunProperties());
        props.AppendChild(new Bold());
        props.AppendChild(new FontSize { Val = "32" });
        run.AppendChild(new Text(text));
    }

    private static void AddHeading(Body body, string text)
    {
        Paragraph para = body.AppendChild(new Paragraph());
        Run run = para.AppendChild(new Run());
        RunProperties props = run.AppendChild(new RunProperties());
        props.AppendChild(new Bold());
        props.AppendChild(new FontSize { Val = "24" });
        run.AppendChild(new Text(text));
    }

    private static void AddParagraph(Body body, string text)
    {
        Paragraph para = body.AppendChild(new Paragraph());
        Run run = para.AppendChild(new Run());
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static void AddFormattedParagraph(Body body, string text, bool bold = false, bool italic = false)
    {
        Paragraph para = body.AppendChild(new Paragraph());
        Run run = para.AppendChild(new Run());
        RunProperties props = run.AppendChild(new RunProperties());

        if (bold)
        {
            props.AppendChild(new Bold());
        }

        if (italic)
        {
            props.AppendChild(new Italic());
        }

        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static void AddBulletListItem(Body body, string text, WordprocessingDocument document)
    {
        EnsureNumberingPart(document);

        Paragraph para = body.AppendChild(new Paragraph());

        // Apply bullet numbering
        ParagraphProperties paraProps = para.AppendChild(new ParagraphProperties());
        NumberingProperties numProps = new NumberingProperties(
            new NumberingLevelReference() { Val = 0 },
            new NumberingId() { Val = 1 } // Bullet list ID
        );
        paraProps.AppendChild(numProps);

        Run run = para.AppendChild(new Run());
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static void AddNumberedListItem(Body body, string text, WordprocessingDocument document)
    {
        EnsureNumberingPart(document);

        Paragraph para = body.AppendChild(new Paragraph());

        // Apply numbered list numbering
        ParagraphProperties paraProps = para.AppendChild(new ParagraphProperties());
        NumberingProperties numProps = new NumberingProperties(
            new NumberingLevelReference() { Val = 0 },
            new NumberingId() { Val = 2 } // Numbered list ID
        );
        paraProps.AppendChild(numProps);

        Run run = para.AppendChild(new Run());
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static void EnsureNumberingPart(WordprocessingDocument document)
    {
        MainDocumentPart mainPart = document.MainDocumentPart!;

        if (mainPart.NumberingDefinitionsPart == null)
        {
            NumberingDefinitionsPart numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
            Numbering numbering = new Numbering();

            // Create bullet list definition (AbstractNum ID 0, Num ID 1)
            AbstractNum bulletAbstractNum = new AbstractNum() { AbstractNumberId = 0 };
            Level bulletLevel = new Level() { LevelIndex = 0 };
            bulletLevel.AppendChild(new NumberingFormat() { Val = NumberFormatValues.Bullet });
            bulletLevel.AppendChild(new LevelText() { Val = "·" });
            bulletLevel.AppendChild(new LevelJustification() { Val = LevelJustificationValues.Left });
            PreviousParagraphProperties bulletPPr = new PreviousParagraphProperties();
            bulletPPr.AppendChild(new Indentation() { Left = "720", Hanging = "360" });
            bulletLevel.AppendChild(bulletPPr);
            bulletAbstractNum.AppendChild(bulletLevel);
            numbering.AppendChild(bulletAbstractNum);

            NumberingInstance bulletNum = new NumberingInstance() { NumberID = 1 };
            bulletNum.AppendChild(new AbstractNumId() { Val = 0 });
            numbering.AppendChild(bulletNum);

            // Create numbered list definition (AbstractNum ID 1, Num ID 2)
            AbstractNum numberedAbstractNum = new AbstractNum() { AbstractNumberId = 1 };
            Level numberedLevel = new Level() { LevelIndex = 0 };
            numberedLevel.AppendChild(new StartNumberingValue() { Val = 1 });
            numberedLevel.AppendChild(new NumberingFormat() { Val = NumberFormatValues.Decimal });
            numberedLevel.AppendChild(new LevelText() { Val = "%1." });
            numberedLevel.AppendChild(new LevelJustification() { Val = LevelJustificationValues.Left });
            PreviousParagraphProperties numberedPPr = new PreviousParagraphProperties();
            numberedPPr.AppendChild(new Indentation() { Left = "720", Hanging = "360" });
            numberedLevel.AppendChild(numberedPPr);
            numberedAbstractNum.AppendChild(numberedLevel);
            numbering.AppendChild(numberedAbstractNum);

            NumberingInstance numberedNum = new NumberingInstance() { NumberID = 2 };
            numberedNum.AppendChild(new AbstractNumId() { Val = 1 });
            numbering.AppendChild(numberedNum);

            numberingPart.Numbering = numbering;
            numberingPart.Numbering.Save();
        }
    }

    private static Table CreateTable(Body body, int rows, int cols)
    {
        Table table = new Table();

        TableProperties props = new TableProperties(
            new TableBorders(
                new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
            )
        );

        table.AppendChild(props);

        for (int r = 0; r < rows; r++)
        {
            TableRow row = new TableRow();
            for (int c = 0; c < cols; c++)
            {
                TableCell cell = new TableCell();
                cell.Append(new Paragraph(new Run(new Text(""))));
                row.Append(cell);
            }
            table.Append(row);
        }

        body.Append(table);
        return table;
    }

    private static void SetCellText(Table table, int row, int col, string text)
    {
        TableRow? tr = table.Elements<TableRow>().ElementAtOrDefault(row);
        TableCell? cell = tr?.Elements<TableCell>().ElementAtOrDefault(col);
        if (cell != null)
        {
            Paragraph? para = cell.Elements<Paragraph>().FirstOrDefault();
            if (para != null)
            {
                para.RemoveAllChildren<Run>();
                para.AppendChild(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            }
        }
    }
}
