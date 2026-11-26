// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace TriasDev.Templify.Builder.Sample;

/// <summary>
/// The product's theme. The customer's "slots" (colours, fonts, logo) come in here; the definitions never hard-code
/// them, so one theme restyles every template.
/// </summary>
public sealed record BrandTheme(Color Ink, Color Muted, Color Accent, Color Line, Color SuccessBackground, Color WarningBackground, FontFace Body, FontFace Heading, ImageSource Logo)
{
    /// <summary>Creates the sample theme with the embedded logo.</summary>
    public static BrandTheme CreateDefault() => new BrandTheme(
        Ink: Color.FromHex("1A1A1A"),
        Muted: Color.FromHex("6B6B6B"),
        Accent: Color.FromHex("1F4E79"),
        Line: Color.FromHex("D4D4D4"),
        SuccessBackground: Color.FromHex("EFF6F1"),
        WarningBackground: Color.FromHex("FDF3E7"),
        Body: new FontFace("Liberation Sans"),
        Heading: new FontFace("Liberation Sans"),
        Logo: ImageSource.Png(SampleResources.Read("logo.png")));
}

/// <summary>The style keys every template of the product shares.</summary>
public static class Styles
{
    public static readonly ParagraphStyleKey Title = new("DocTitle", "Document Title");
    public static readonly ParagraphStyleKey Subtitle = new("DocSubtitle", "Document Subtitle");
    public static readonly ParagraphStyleKey Heading = new("SectionHeading", "Section Heading");
    public static readonly ParagraphStyleKey Body = new("BodyCopy", "Body Copy");
    public static readonly ParagraphStyleKey TableText = new("TableText", "Table Text");
    public static readonly ParagraphStyleKey TableHeader = new("TableHeader", "Table Header");
    public static readonly ParagraphStyleKey Small = new("SmallPrint", "Small Print");
    public static readonly ParagraphStyleKey ListItem = new("ListItem", "List Item");
    public static readonly ParagraphStyleKey Logo = new("Logo", "Logo");
    public static readonly CharacterStyleKey Strong = new("StrongText", "Strong Text");
    public static readonly CharacterStyleKey Muted = new("MutedText", "Muted Text");
    public static readonly TableStyleKey Grid = new("LineGrid");
    public static readonly BoxStyleKey Success = new("SuccessBox");
    public static readonly BoxStyleKey Warning = new("WarningBox");
    public static readonly ListStyleKey Bullets = new("Bullets");

    /// <summary>The style sheet, built from the theme.</summary>
    public static StyleSheet Create(BrandTheme t) => StyleSheet.Create(s =>
    {
        // Paragraphs without a style are body copy; table cells get the table style's text styles.
        s.Defaults(d => d.Font(t.Body).Size(10.Pt()).Color(t.Ink).Paragraph(Body));
        s.Paragraph(Body, p => p.SpaceAfter(6.Pt()).LineSpacing(1.15));
        s.Paragraph(Title, p => p.BasedOn(Body).Next(Subtitle).Font(t.Heading).Size(22.Pt()).Bold().Color(t.Accent).SpaceAfter(2.Pt()));
        s.Paragraph(Subtitle, p => p.BasedOn(Body).Next(Body).Size(11.Pt()).Color(t.Muted).SpaceAfter(14.Pt()));
        s.Paragraph(Heading, p => p.BasedOn(Body).Next(Body).Font(t.Heading).Size(13.Pt()).Bold().Color(t.Accent)
            .SpaceBefore(14.Pt()).SpaceAfter(6.Pt()).KeepWithNext().OutlineLevel(1));
        s.Paragraph(TableText, p => p.BasedOn(Body).SpaceAfter(0.Pt()));
        s.Paragraph(TableHeader, p => p.BasedOn(TableText).Bold());
        s.Paragraph(Small, p => p.BasedOn(Body).Size(8.Pt()).Color(t.Muted));
        s.Paragraph(ListItem, p => p.BasedOn(Body).SpaceAfter(2.Pt()));
        s.Paragraph(Logo, p => p.SpaceAfter(0.Pt()));
        s.Character(Strong, c => c.Bold());
        s.Character(Muted, c => c.Color(t.Muted));
        s.Table(Grid, g => g
            .HeaderBackground(Color.FromHex("F2F5F8"))
            .HeaderRule(1.Pt(), t.Accent)
            .RowRule(0.5.Pt(), t.Line)
            .CellPadding(3.Pt(), 5.Pt())
            .TextStyle(TableText)
            .HeaderTextStyle(TableHeader));
        s.Box(Success, b => b.Border(0.75.Pt(), Color.FromHex("96C6A8")).Background(t.SuccessBackground).Padding(6.Pt(), 8.Pt()).SpaceAfter(8.Pt()));
        s.Box(Warning, b => b.Border(0.75.Pt(), Color.FromHex("E3B27A")).Background(t.WarningBackground).Padding(6.Pt(), 8.Pt()).SpaceAfter(8.Pt()));
        s.List(Bullets, l => l.ItemStyle(ListItem).Level(0, "•", 6.Mm(), 4.Mm()).Level(1, "–", 12.Mm(), 4.Mm()));
    });
}

/// <summary>
/// A reusable block: any template that prints an address uses it. A record whose parameters are paths, so a
/// <see cref="ComponentCatalog"/> can write it to JSON as a reference.
/// </summary>
public sealed record AddressBlock(TemplatePath Address) : ITemplateComponent
{
    public void Compose(BlockComposer c)
    {
        c.Paragraph(x =>
        {
            x.Value(Address["street"]);
            x.LineBreak();
            x.Value(Address["postalCode"]);
            x.Text(" ");
            x.Value(Address["city"]);
            x.If(Address["country"].NotEq("DE"), t => t.Text(", ").Value(Address["country"]));
        });
    }
}

/// <summary>
/// An order confirmation that uses every construct of the builder: styles, page setup with headers and footers,
/// texts in two languages, values with formats, conditionals (block, switch, inline, expressions), loops (paragraphs,
/// table rows, list items), tables, boxes, rules, lists, images, a table of contents, components and a raw block.
/// </summary>
public sealed class OrderConfirmation(BrandTheme theme) : ITemplateDefinition
{
    public TemplateIdentity Identity { get; } = new("order-confirmation", 1);

    public void Compose(TemplateComposer template)
    {
        TemplateTexts t = template.Texts;
        TemplatePath order = template.Data["order"];

        template.Styles(Styles.Create(theme));
        template.Properties(p => p.Title(t["title"]).Author("Example Trading").Subject(t["title"]));

        template.Page(page => page
            .Size(PageSize.A4)
            .Margins(25.Mm(), 20.Mm(), 22.Mm(), 20.Mm())
            .Header(h => h.Paragraph(Styles.Small, x =>
            {
                x.Text(t["title"] + " ");
                x.Value(order["number"]);
                x.TabRight();
                x.Text(t["page"] + " ");
                x.PageNumber();
                x.Text(" / ");
                x.PageCount();
            }))
            .Footer(f => f.Paragraph(Styles.Small, t["footer"]))
            .FirstPage(first =>
            {
                // The first page carries the logo instead of the running header.
                first.Header(h => h.Paragraph(Styles.Logo, x => x.Image(theme.Logo, "Example Trading", ImageFit.Height(12.Mm()))));
                first.Footer(f => f.Paragraph(Styles.Small, t["footer"]));
            }));

        template.Content(c =>
        {
            c.Paragraph(Styles.Title, t["title"]);
            c.Paragraph(Styles.Subtitle, x =>
            {
                x.Text(t["orderNo"] + " ");
                x.Value(order["number"], Styles.Strong);
                x.Text(" · ");
                x.Value(order["date"], ValueFormat.Date(t["dateFormat"]));
            });

            c.TableOfContents(1);

            Customer(c, t, order);
            Items(c, t, order);
            Status(c, t, order);
            Checks(c, t, order);
            Notes(c, t, order);

            c.PageBreak();
            c.Paragraph(Styles.Heading, t["terms"]);
            c.Paragraph(Styles.Small, t["termsText"]);
            c.Rule(0.5.Pt(), theme.Line, 12.Pt(), 4.Pt());
            c.Raw(PrintedOn(t["printedOn"]));
        });
    }

    private static void Customer(BlockComposer c, TemplateTexts t, TemplatePath order)
    {
        c.Paragraph(Styles.Heading, t["customer"]);
        c.Paragraph(x =>
        {
            x.Value(order["customer.name"], Styles.Strong);
            x.If(order["customer.isNew"], s => s.Text(" (" + t["newCustomer"] + ")", Styles.Muted));
        });
        c.If(order["customer.contact"].IsNotEmpty, b => b.Paragraph(x =>
        {
            x.Text(t["attn"] + " ", Styles.Muted);
            x.Value(order["customer.contact"]);
        }));
        c.Component(new AddressBlock(order["customer.address"]));
    }

    private void Items(BlockComposer c, TemplateTexts t, TemplatePath order)
    {
        c.Paragraph(Styles.Heading, t["items"]);
        c.Table(table =>
        {
            table.Style(Styles.Grid).SpaceAfter(8.Pt());
            table.Columns(cols =>
            {
                cols.Fixed(10.Mm());
                cols.Relative(4);
                cols.Fixed(16.Mm());
                cols.Fixed(30.Mm());
            });
            table.HeaderRow(r =>
            {
                r.Cell("#");
                r.Cell(t["description"]);
                r.Cell(t["quantity"]);
                r.Cell(t["amount"]);
            });
            table.ForEachRow(order["lines"], "line", (line, rows) =>
            {
                rows.Row(r =>
                {
                    r.Cell(line.Number);
                    r.Cell(cell =>
                    {
                        cell.Paragraph(x =>
                        {
                            x.Value(line["description"]);
                            x.Text(" — ", Styles.Muted);
                            x.If(line["inStock"], s => s.Text(t["inStock"], Styles.Muted)).Else(s => s.Text(t["backOrdered"], Styles.Muted));
                        });
                        cell.If(line["tags"].IsNotEmpty, b => b.Paragraph(Styles.Small, x => x.Value(line["tags"], ValueFormat.Join(separator: " · "))));
                    });
                    r.Cell(line["quantity"]);
                    r.Cell(cell => cell.Paragraph(x => x.Value(line["amount"], ValueFormat.Currency)));
                });
            });
            table.RowsIf(order["discount"].Gt(0), rows => rows.Row(r =>
            {
                r.Cell(cell => cell.ColumnSpan(3).Paragraph(t["discount"]));
                r.Cell(cell => cell.Paragraph(x => x.Value(order["discount"], ValueFormat.Currency)));
            }));
            table.Row(r =>
            {
                r.TextStyle(Styles.TableHeader);
                r.Cell(cell => cell.ColumnSpan(3).Paragraph(t["total"]));
                r.Cell(cell => cell.Paragraph(x => x.Value(order["total"], ValueFormat.Currency)));
            });
        });

        c.Paragraph(x =>
        {
            x.Text(t["freeExpress"] + " ");
            x.Expression(order["isExpress"] & order["total"].Gt(1000), ValueFormat.YesNo);
        });
    }

    private static void Status(BlockComposer c, TemplateTexts t, TemplatePath order)
    {
        // Every unknown status falls into Default.
        c.Switch(order["status"], s =>
        {
            s.Case("paid", b => b.Box(Styles.Success, x => x.Paragraph(t["statusPaid"])));
            s.Case(new[] { "overdue", "reminded" }, b => b.Box(Styles.Warning, x => x.Paragraph(t["statusOverdue"])));
            s.Default(b => b.Paragraph(t["statusOpen"]));
        });

        c.If(order["isExpress"] & order["total"].Gt(1000), b => b.Paragraph(t["expressFree"]))
         .ElseIf(order["isExpress"], b => b.Paragraph(t["express"]))
         .Else(b => b.Paragraph(t["standard"]));
    }

    private static void Checks(BlockComposer c, TemplateTexts t, TemplatePath order)
    {
        c.If(order["checks"].IsNotEmpty, b =>
        {
            b.Paragraph(Styles.Heading, t["checks"]);
            b.List(Styles.Bullets, list => list.ForEach(order["checks"], "check", (check, items) =>
            {
                items.Item(x =>
                {
                    x.Value(check["title"]);
                    x.Text(": ");
                    x.Value(check["passed"], ValueFormat.Checkmark);
                });
                items.If(check["note"].Exists, notes => notes.Item(1, check["note"]));
            }));
        });
    }

    private static void Notes(BlockComposer c, TemplateTexts t, TemplatePath order)
    {
        c.If(order["notes"].IsNotEmpty, b =>
        {
            b.Paragraph(Styles.Heading, t["notes"]);
            b.Paragraph(order["notes"]);
        });
    }

    /// <summary>
    /// A raw block: a date field the application fills when it prints. The builder has no date-field construct, so
    /// the XML is written per format.
    /// </summary>
    internal static RawBlock PrintedOn(string label)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        return new RawBlock
        {
            Docx = _ => new XElement(w + "p",
                new XElement(w + "pPr", new XElement(w + "pStyle", new XAttribute(w + "val", Styles.Small.Id))),
                new XElement(w + "r", new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), label + " ")),
                new XElement(w + "fldSimple", new XAttribute(w + "instr", " DATE \\@ \"yyyy-MM-dd\" "),
                    new XElement(w + "r", new XElement(w + "t", "2026-01-01")))),
            Odt = _ => new XElement(text + "p", new XAttribute(text + "style-name", Styles.Small.Id),
                label + " ",
                new XElement(text + "date", "2026-01-01")),
        };
    }
}
