# TriasDev.Templify.Builder

> **Preview (0.x).** The API may change between minor versions until the first stable release.

Describe Templify templates in C# and generate them at runtime as Word (`.docx`) or OpenDocument Text (`.odt`) files.
[TriasDev.Templify](https://www.nuget.org/packages/TriasDev.Templify) then fills the generated templates with data.

```csharp
using System.Globalization;
using TriasDev.Templify.Builder;
using TriasDev.Templify.Core;

public sealed class GreetingTemplate : ITemplateDefinition
{
    public TemplateIdentity Identity { get; } = new("greeting", 1);

    public void Compose(TemplateComposer template)
    {
        TemplatePath customer = template.Data["customer"];

        template.Content(c =>
        {
            c.Paragraph(x =>
            {
                x.Text("Hello ");
                x.Value(customer["name"]);
                x.Text("!");
            });
        });
    }
}

GeneratedTemplate generated = TemplateBuilder.Build(
    new GreetingTemplate(),
    new TemplateBuildOptions { Format = TemplateFormat.Odt, Language = CultureInfo.GetCultureInfo("en-US") });

// Hand the template to a user as a download, or fill it right away:
using Stream template = generated.OpenRead();
using MemoryStream output = new MemoryStream();
new TemplateProcessor().ProcessTemplate(template, output, """{ "customer": { "name": "Alice" } }""");
```

The same definition builds both formats. The output is deterministic: the same definition and options produce the
same bytes, so `GeneratedTemplate.ContentHash` can serve as a cache key.

The design and the roadmap are in the
[design spec](https://github.com/TriasDev/templify/blob/main/docs/superpowers/specs/2026-11-25-template-builder-design.md)
and in [epic #261](https://github.com/TriasDev/templify/issues/261).
