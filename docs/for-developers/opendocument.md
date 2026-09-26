# OpenDocument (.odt / .ott)

Templify processes OpenDocument Text templates (`.odt`, and `.ott` templates) from LibreOffice, Collabora Online and
Apache OpenOffice. They use the same template syntax, `PlaceholderReplacementOptions`, `ProcessingResult` and
warnings as Word templates, and need no extra package and no LibreOffice installation.

There are two entry points in `TriasDev.Templify.Core`:

| Class | Use it when |
|---|---|
| `OdtTemplateProcessor` | You know the templates are OpenDocument Text files. |
| `TemplateProcessor` | Templates can be Word **or** OpenDocument files. The format is detected from the file content, and the call is passed to `DocumentTemplateProcessor` or `OdtTemplateProcessor`. |

For template authors, [LibreOffice / OpenDocument Templates](../for-template-authors/libreoffice.md) describes how
to write templates in LibreOffice Writer and what behaves differently from Word.

## OdtTemplateProcessor

`OdtTemplateProcessor` has the same methods as `DocumentTemplateProcessor`: `ProcessTemplate` for streams and byte
arrays, `ProcessTemplateFile`, and `ValidateTemplate`. Each takes data as a `Dictionary<string, object>`, an
`IReadOnlyDictionary<string, object?>` or a JSON string.

```csharp
using TriasDev.Templify.Core;

var data = new Dictionary<string, object>
{
    ["Name"] = "John Doe",
    ["Items"] = new List<object>
    {
        new { Product = "Service A", Price = 100m },
        new { Product = "Service B", Price = 200m }
    }
};

var processor = new OdtTemplateProcessor();

// File to file: the output file is written only when processing succeeds.
ProcessingResult result = processor.ProcessTemplateFile("template.odt", "output.odt", data);

if (!result.IsSuccess)
{
    Console.WriteLine($"Failed: {result.ErrorMessage}");
}
```

An `.ott` template produces an `.odt` document: the package media type is changed to OpenDocument Text.

### Streams and Byte Arrays

```csharp
// Byte arrays: `output` is empty when processing fails.
byte[] template = File.ReadAllBytes("template.odt");
ProcessingResult bytesResult = processor.ProcessTemplate(template, data, out byte[] output);

// Streams: the output stream only has to be writable.
using var templateStream = File.OpenRead("template.ott");
using var outputStream = File.Create("output.odt"); // creates or truncates the file
ProcessingResult streamResult = processor.ProcessTemplate(templateStream, outputStream, data);
```

The requirements are looser than for Word documents:

- The template stream must be readable. It does not have to be seekable.
- The output stream only has to be **writable**; it does not have to be readable or seekable. The document is built
  in memory and written in one go, and **only when processing succeeds**. On failure nothing is written.
- To write a file, open it with **`File.Create`**, which creates the file or truncates an existing one.
  `File.OpenWrite` does not truncate: it writes over an existing file from the start and keeps any bytes after the
  new content, which leaves a corrupt ZIP file when the old file was longer. Templify cuts off seekable outputs after
  the document (see [Memory Use](#memory-use)), but `File.Create` does not depend on that.

A template that is not an OpenDocument Text package is reported as a failed result, with an `ErrorMessage` that
names what was found. This covers a Word file, a spreadsheet, a flat `.fodt` file or a password-protected document.

### Memory Use

Only the parts that processing reads are unpacked into memory: `content.xml`, `styles.xml`, `meta.xml` and
`META-INF/manifest.xml`. Pictures, embedded objects and all other entries are copied from the template into the
output entry by entry, without holding them in memory. The output package is built in memory in compressed form (so
that nothing is written on failure), and a template stream that is not seekable is buffered in compressed form. Memory
use therefore follows the size of the `.odt` file plus the size of its XML parts, not the unpacked size of its pictures.

Each XML part may be at most 256 MB when unpacked, and a package at most 65,535 entries. A template that exceeds
these limits fails with an `ErrorMessage` such as `content.xml exceeds the maximum supported size`. Real documents
are far below these limits. Treat templates from untrusted sources with care anyway: an entry that unpacks to a very
large size is not held in memory, but it still costs time to copy.

When the output stream is seekable (a file or a `MemoryStream`), it is cut off after the written document, so even
an existing, longer file opened with `File.OpenWrite` does not keep bytes of its earlier content. Prefer
`File.Create` anyway.

### Options

All `PlaceholderReplacementOptions` apply, with two notes:

- `UpdateFieldsOnOpen` is ignored. LibreOffice updates page and date fields when it opens a document, and
  OpenDocument has no portable "update on open" setting.
- `DocumentProperties` are written to `meta.xml`: `Author` becomes the initial creator, `LastModifiedBy` the creator,
  `Title`, `Subject` and `Description` their `dc:` fields, `Keywords` a keyword, and `Category` a user-defined
  property named `Category`.

### Validation

```csharp
using var stream = File.OpenRead("template.odt");
ValidationResult validation = processor.ValidateTemplate(stream, data);

if (!validation.IsValid)
{
    foreach (ValidationError error in validation.Errors)
    {
        Console.WriteLine($"{error.Type}: {error.Message}");
    }
}

Console.WriteLine($"Missing: {string.Join(", ", validation.MissingVariables)}");
```

## TemplateProcessor: Word and OpenDocument

`TemplateProcessor` accepts both formats through one API. It has the same methods as the two processors.

```csharp
using TriasDev.Templify.Core;

var processor = new TemplateProcessor();

// Works for .docx, .odt and .ott templates; the output has the format of the template.
ProcessingResult result = processor.ProcessTemplateFile(templatePath, outputPath, data);
```

The format is detected from the package content, not from the file name:

- An OpenDocument package names its media type in the `mimetype` entry: `application/vnd.oasis.opendocument.text`, or
  `…-text-template` for `.ott`.
- A Word package declares a WordprocessingML main document in `[Content_Types].xml`. This covers `.docx` and also
  `.docm`, `.dotx` and `.dotm`.

To choose an output file name, or to check a file before processing, call `DetectFormat`:

```csharp
using var template = File.OpenRead(templatePath);

TemplateFormat format = TemplateProcessor.DetectFormat(template);  // the stream position is restored
string extension = format switch
{
    TemplateFormat.Docx => ".docx",
    TemplateFormat.Odt => ".odt",
    _ => throw new NotSupportedException("Not a Word or OpenDocument Text template.")
};
```

Behavior:

- Results, warnings, exceptions and option handling are those of the processor the call is passed to.
- The output stream requirements depend on the format. A Word template needs a readable, writable and seekable output
  stream, and an OpenDocument template only a writable one. A `MemoryStream`, or `File.Create`, works for both.
- A template stream that is not seekable is copied into memory first, because the format must be known before
  processing. `DetectFormat` itself needs a readable, seekable stream.
- A template in any other format is a failed result (`ErrorMessage` starts with "Unsupported template format"), and
  `ValidateTemplate` returns an invalid result. Examples are a legacy `.doc`, a flat `.fodt`, a spreadsheet, an empty
  file or random bytes. Nothing is written to the output.

`DocumentTemplateProcessor` is unchanged. It still only processes Word documents.
