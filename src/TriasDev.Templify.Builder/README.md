# TriasDev.Templify.Builder

> **Preview (0.x).** The API may change between minor versions until the first stable release.

Describe Templify templates in C# and generate them at runtime as Word (`.docx`) or OpenDocument Text (`.odt`)
files. [TriasDev.Templify](https://www.nuget.org/packages/TriasDev.Templify) then fills the generated templates with
data.

- **One definition, every format and language.** Named styles from your theme, texts from a catalog, DOCX and ODT
  from the same code.
- **Invalid template syntax cannot be written.** Markers, conditions, loops and value formats come from the API, in
  the shapes the engine supports.
- **A data contract and an identity stamp.** Every build lists the data it needs, and an uploaded, hand-edited copy
  can be checked against it.

```csharp
using System.Globalization;
using TriasDev.Templify.Builder;
using TriasDev.Templify.Core;

public sealed class InvoiceTemplate(BrandTheme theme) : ITemplateDefinition
{
    private static readonly ParagraphStyleKey Title = new("Title1", "Invoice Title");
    private static readonly ParagraphStyleKey Body = new("Body", "Body Copy");
    private static readonly CharacterStyleKey Strong = new("Strong", "Strong Text");

    public TemplateIdentity Identity { get; } = new("invoice", 1);

    public void Compose(TemplateComposer template)
    {
        TemplateTexts t = template.Texts;
        TemplatePath order = template.Data["order"];

        template.Styles(StyleSheet.Create(s =>
        {
            s.Defaults(d => d.Font(theme.BodyFont).Size(10.5.Pt()).Color(theme.Ink));
            s.Paragraph(Body, p => p.SpaceAfter(6.Pt()));
            s.Paragraph(Title, p => p.BasedOn(Body).Size(18.Pt()).Color(theme.Accent).OutlineLevel(1));
            s.Character(Strong, c => c.Bold());
        }));
        template.Properties(p => p.Title(t["invoice"]));

        template.Content(c =>
        {
            c.Paragraph(Title, x => { x.Text(t["invoice"] + " "); x.Value(order["number"]); });
            c.ForEach(order["lines"], "line", (line, b) => b.Paragraph(Body, x =>
            {
                x.Value(line["description"]);
                x.Tab();
                x.Value(line["amount"], ValueFormat.Currency, Strong);
            }));
            c.Switch(order["status"], s =>
            {
                s.Case("paid", b => b.Paragraph(Body, t["paid"]));
                s.Default(b => b.Paragraph(Body, t["open"]));
            });
        });
    }
}

GeneratedTemplate template = TemplateBuilder.Build(
    new InvoiceTemplate(theme),
    new TemplateBuildOptions { Format = TemplateFormat.Odt, Language = CultureInfo.GetCultureInfo("de-DE"), Texts = texts });

// Hand it to a user as a download (template.OpenRead(), template.MediaType, "invoice" + template.FileExtension)
// or fill it right away:
using MemoryStream output = new MemoryStream();
new TemplateProcessor().ProcessTemplate(template.OpenRead(), output, json);

// Later, check a copy the user edited and uploaded:
UploadCheckResult check = TemplateInspector.CheckUpload(upload, template.Contract, new TemplateIdentity("invoice", 1));
```

The output is deterministic: the same definition and options produce the same bytes, so
`GeneratedTemplate.ContentHash` can serve as a cache key.

The design and the roadmap are in the
[design spec](https://github.com/TriasDev/templify/blob/main/docs/superpowers/specs/2026-11-25-template-builder-design.md)
and in [epic #261](https://github.com/TriasDev/templify/issues/261).
