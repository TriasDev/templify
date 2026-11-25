# TriasDev.Templify.Builder.Testing

Test kit for [TriasDev.Templify.Builder](https://www.nuget.org/packages/TriasDev.Templify.Builder). The builder
generates templates at runtime, so your CI is where your template definitions get verified. The kit does not depend
on a test framework: call it from xUnit, NUnit or MSTest.

```csharp
[Fact]
public void InvoiceTemplate_IsValid() => TemplateVerifier.VerifyAll(
    () => new InvoiceTemplate(theme),
    new TemplateVerificationOptions
    {
        Languages = [CultureInfo.GetCultureInfo("de-DE"), CultureInfo.GetCultureInfo("en-US")],
        Texts = language => catalogs[language.Name],
        SampleData = [File.ReadAllText("invoice-sample.json")],
    });
```

`VerifyAll` builds the definition for every language and format and throws `TemplateVerificationException`, listing
every failure, when one of these checks fails:

- the definition builds;
- `ValidateTemplate` reports no errors;
- the data contract is complete: filling with the contract's witness raises no missing variable and no warning;
- every sample data document fills without missing variables and without warnings;
- the DOCX passes the OOXML schema validation, the ODT the package checks (template and filled documents);
- a second build gives the same bytes, also under the current cultures tr-TR and ar-SA;
- all languages and formats have the same data contract.

`Verify` returns the same `TemplateVerificationReport` without throwing. `CheckTemplate`, `CheckFill` and
`CheckPackage` run single checks.

## Snapshots

`TemplateSnapshot.Create(template)` returns a canonical text dump (paragraphs with their styles, markers, tables,
headers and footers) for golden tests:

```text
== body
Heading1 | Order {{order.number}}
TemplifyMarker | {{#if order.isPaid}}
Normal | Paid.
TemplifyMarker | {{/if}}
```

## LibreOffice

`LibreOfficeConverter` converts templates and filled documents with a local LibreOffice (`TEMPLIFY_SOFFICE` or the
default install path). Check `LibreOfficeConverter.IsAvailable` and skip the test otherwise.
