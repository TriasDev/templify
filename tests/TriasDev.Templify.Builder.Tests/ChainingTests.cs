// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Content-adding methods return their composer, and styles come first (#293).</summary>
public sealed class ChainingTests
{
    private static readonly CharacterStyleKey _strong = new CharacterStyleKey("Strong", "Strong Text");
    private static readonly ListStyleKey _bullets = new ListStyleKey("Bullets");

    private static void Styles(TemplateComposer t) => t.Styles(StyleSheet.Create(s =>
    {
        s.Character(_strong, c => c.Bold());
        s.List(_bullets, l => l.Level(0, "•", 6.Mm(), 4.Mm()).Level(1, "–", 12.Mm(), 4.Mm()));
    }));

    [Theory]
    [InlineData(TemplateFormat.Docx)]
    [InlineData(TemplateFormat.Odt)]
    public void Chained_BuildsLikeStatements(TemplateFormat format)
    {
        TestDefinition chained = new TestDefinition(t =>
        {
            Styles(t);
            t.Content(c => c
                .Paragraph("Title")
                .Paragraph(x => x.Text("Dear ").Value(_strong, t.Data["name"]).Text(_strong, "!").Tab().Value(t.Data["total"], ValueFormat.Currency))
                .List(_bullets, l => l.Item("one").Item(1, "two").ForEach(t.Data["tags"], "tag", (tag, i) => i.Item(tag.Self)))
                .Table(table => table
                    .Columns(k => k.Relative(1).Relative(1))
                    .HeaderRow(r => r.Cell("A").Cell("B"))
                    .Row(r => r.Cell("1").Cell(c => c.Paragraph("2").Paragraph("3")))
                    .ForEachRow(t.Data["lines"], "line", (line, rows) => rows.Row(r => r.Cell(line["name"]).Cell(line["qty"]))))
                .Switch(t.Data["status"], s => s.Case("paid", b => b.Paragraph("Paid").Paragraph("Thanks")).Default(b => b.Paragraph("Open")))
                .PageBreak()
                .Paragraph("End"));
        });
        TestDefinition statements = new TestDefinition(t =>
        {
            Styles(t);
            t.Content(c =>
            {
                c.Paragraph("Title");
                c.Paragraph(x =>
                {
                    x.Text("Dear ");
                    x.Value(_strong, t.Data["name"]);
                    x.Text(_strong, "!");
                    x.Tab();
                    x.Value(t.Data["total"], ValueFormat.Currency);
                });
                c.List(_bullets, l =>
                {
                    l.Item("one");
                    l.Item(1, "two");
                    l.ForEach(t.Data["tags"], "tag", (tag, i) => i.Item(tag.Self));
                });
                c.Table(table =>
                {
                    table.Columns(k =>
                    {
                        k.Relative(1);
                        k.Relative(1);
                    });
                    table.HeaderRow(r =>
                    {
                        r.Cell("A");
                        r.Cell("B");
                    });
                    table.Row(r =>
                    {
                        r.Cell("1");
                        r.Cell(c =>
                        {
                            c.Paragraph("2");
                            c.Paragraph("3");
                        });
                    });
                    table.ForEachRow(t.Data["lines"], "line", (line, rows) => rows.Row(r =>
                    {
                        r.Cell(line["name"]);
                        r.Cell(line["qty"]);
                    }));
                });
                c.Switch(t.Data["status"], s =>
                {
                    s.Case("paid", b =>
                    {
                        b.Paragraph("Paid");
                        b.Paragraph("Thanks");
                    });
                    s.Default(b => b.Paragraph("Open"));
                });
                c.PageBreak();
                c.Paragraph("End");
            });
        });

        GeneratedTemplate template = BuilderHarness.Build(chained, format);

        Assert.Equal(BuilderHarness.Build(statements, format).ContentHash, template.ContentHash);
        BuilderHarness.AssertInvariants(chained, template);
    }
}
