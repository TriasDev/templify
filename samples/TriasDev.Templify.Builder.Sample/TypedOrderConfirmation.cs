// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder.Sample;

/// <summary>
/// The address block of <see cref="AddressBlock"/> with a typed parameter: the component navigates the address with
/// the compiler's help and builds the same output.
/// </summary>
public sealed record OrderAddressBlock(TemplatePath<Address> Address) : ITemplateComponent
{
    public void Compose(BlockComposer c)
    {
        c.Paragraph(x =>
        {
            x.Value(Address.Street);
            x.LineBreak();
            x.Value(Address.PostalCode);
            x.Text(" ");
            x.Value(Address.City);
            x.If(Address.Country.NotEq("DE"), t => t.Text(", ").Value(Address.Country));
        });
    }
}

/// <summary>
/// The order confirmation of <see cref="OrderConfirmation"/>, with data paths from <see cref="OrderData"/> instead of
/// strings: <c>order.Number</c>, <c>line.Quantity</c> are generated from the data classes (TriasDev.Templify.Builder's
/// source generator). It builds byte for byte the same templates.
/// </summary>
public sealed class TypedOrderConfirmation(BrandTheme theme) : ITemplateDefinition
{
    public TemplateIdentity Identity { get; } = new("order-confirmation", 1);

    public void Compose(TemplateComposer template)
    {
        TemplateTexts t = template.Texts;
        TemplatePath<Order> order = template.DataAs<OrderData>().Order;

        template.Styles(Styles.Create(theme));
        template.Properties(p => p.Title(t["title"]).Author("Example Trading").Subject(t["title"]));

        template.Page(page => page
            .Size(PageSize.A4)
            .Margins(25.Mm(), 20.Mm(), 22.Mm(), 20.Mm())
            .Header(h => h.Paragraph(Styles.Small, x =>
            {
                x.Text(t["title"] + " ");
                x.Value(order.Number);
                x.TabRight();
                x.Text(t["page"] + " ");
                x.PageNumber();
                x.Text(" / ");
                x.PageCount();
            }))
            .Footer(f => f.Paragraph(Styles.Small, t["footer"]))
            .FirstPage(first =>
            {
                first.Header(h => h.Paragraph(Styles.Logo, x => x.Image(theme.Logo, "Example Trading", ImageFit.Height(12.Mm()))));
                first.Footer(f => f.Paragraph(Styles.Small, t["footer"]));
            }));

        template.Content(c =>
        {
            c.Paragraph(Styles.Title, t["title"]);
            c.Paragraph(Styles.Subtitle, x =>
            {
                x.Text(t["orderNo"] + " ");
                x.Value(order.Number, Styles.Strong);
                x.Text(" · ");
                x.Value(order.Date, ValueFormat.Date(t["dateFormat"]));
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
            c.Raw(OrderConfirmation.PrintedOn(t["printedOn"]));
        });
    }

    private static void Customer(BlockComposer c, TemplateTexts t, TemplatePath<Order> order)
    {
        TemplatePath<Customer> customer = order.Customer;
        c.Paragraph(Styles.Heading, t["customer"]);
        c.Paragraph(x =>
        {
            x.Value(customer.Name, Styles.Strong);
            x.If(customer.IsNew, s => s.Text(" (" + t["newCustomer"] + ")", Styles.Muted));
        });
        c.If(customer.Contact.IsNotEmpty, b => b.Paragraph(x =>
        {
            x.Text(t["attn"] + " ", Styles.Muted);
            x.Value(customer.Contact);
        }));
        c.Component(new OrderAddressBlock(customer.Address));
    }

    private static void Items(BlockComposer c, TemplateTexts t, TemplatePath<Order> order)
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
            table.ForEachRow(order.Lines, "line", (line, rows) =>
            {
                rows.Row(r =>
                {
                    r.Cell(line.Number);
                    r.Cell(cell =>
                    {
                        cell.Paragraph(x =>
                        {
                            x.Value(line.Description);
                            x.Text(" — ", Styles.Muted);
                            x.If(line.InStock, s => s.Text(t["inStock"], Styles.Muted)).Else(s => s.Text(t["backOrdered"], Styles.Muted));
                        });
                        cell.If(line.Tags.IsNotEmpty, b => b.Paragraph(Styles.Small, x => x.Value(line.Tags, ValueFormat.Join(separator: " · "))));
                    });
                    r.Cell(line.Quantity);
                    r.Cell(cell => cell.Paragraph(x => x.Value(line.Amount, ValueFormat.Currency)));
                });
            });
            table.RowsIf(order.Discount.Gt(0), rows => rows.Row(r =>
            {
                r.Cell(cell => cell.ColumnSpan(3).Paragraph(t["discount"]));
                r.Cell(cell => cell.Paragraph(x => x.Value(order.Discount, ValueFormat.Currency)));
            }));
            table.Row(r =>
            {
                r.TextStyle(Styles.TableHeader);
                r.Cell(cell => cell.ColumnSpan(3).Paragraph(t["total"]));
                r.Cell(cell => cell.Paragraph(x => x.Value(order.Total, ValueFormat.Currency)));
            });
        });

        c.Paragraph(x =>
        {
            x.Text(t["freeExpress"] + " ");
            x.Expression(order.IsExpress & order.Total.Gt(1000), ValueFormat.YesNo);
        });
    }

    private static void Status(BlockComposer c, TemplateTexts t, TemplatePath<Order> order)
    {
        c.Switch(order.Status, s =>
        {
            s.Case("paid", b => b.Box(Styles.Success, x => x.Paragraph(t["statusPaid"])));
            s.Case(new[] { "overdue", "reminded" }, b => b.Box(Styles.Warning, x => x.Paragraph(t["statusOverdue"])));
            s.Default(b => b.Paragraph(t["statusOpen"]));
        });

        Condition expressFree = order.IsExpress & order.Total.Gt(1000);
        c.If(expressFree, b => b.Paragraph(t["expressFree"]))
         .ElseIf(order.IsExpress, b => b.Paragraph(t["express"]))
         .Else(b => b.Paragraph(t["standard"]));
    }

    private static void Checks(BlockComposer c, TemplateTexts t, TemplatePath<Order> order)
    {
        TemplateCollection<Check> checks = order.Checks;
        c.If(checks.IsNotEmpty, b =>
        {
            b.Paragraph(Styles.Heading, t["checks"]);
            b.List(Styles.Bullets, list => list.ForEach(checks, "check", (check, items) =>
            {
                items.Item(x =>
                {
                    x.Value(check.Title);
                    x.Text(": ");
                    x.Value(check.Passed, ValueFormat.Checkmark);
                });
                items.If(check.Note.Exists, notes => notes.Item(1, check.Note));
            }));
        });
    }

    private static void Notes(BlockComposer c, TemplateTexts t, TemplatePath<Order> order)
    {
        c.If(order.Notes.IsNotEmpty, b =>
        {
            b.Paragraph(Styles.Heading, t["notes"]);
            b.Paragraph(order.Notes);
        });
    }
}
