// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Sample;
using TriasDev.Templify.Builder.Testing;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Runs the sample (samples/TriasDev.Templify.Builder.Sample), so CI keeps it working (#287).</summary>
public sealed class SampleTests
{
    [Fact]
    public void Sample_VerifiesBuildsAndFillsEveryLanguageAndFormat()
    {
        string directory = Path.Combine(Path.GetTempPath(), "templify-sample-" + Guid.NewGuid().ToString("N"));
        try
        {
            StringWriter log = new StringWriter();

            IReadOnlyList<string> files = SampleRunner.Run(directory, log);

            Assert.Equal(
                new[]
                {
                    "order-confirmation.de-DE.template.docx", "order-confirmation.de-DE.filled.docx",
                    "order-confirmation.de-DE.template.odt", "order-confirmation.de-DE.filled.odt",
                    "order-confirmation.en-GB.template.docx", "order-confirmation.en-GB.filled.docx",
                    "order-confirmation.en-GB.template.odt", "order-confirmation.en-GB.filled.odt",
                    "data-contract.json",
                },
                files.Select(Path.GetFileName));
            Assert.All(files, f => Assert.True(new FileInfo(f).Length > 0, f));
            Assert.Contains("Template verification passed (4 templates).", log.ToString(), StringComparison.Ordinal);
            Assert.Contains("usable=True, origin=CurrentContract", log.ToString(), StringComparison.Ordinal);

            byte[] filled = File.ReadAllBytes(Path.Combine(directory, "order-confirmation.en-GB.filled.odt"));
            string snapshot = TemplateSnapshot.Create(TemplateFormat.Odt, filled);
            Assert.Contains("DocTitle | Order confirmation", snapshot, StringComparison.Ordinal);
            Assert.Contains("TableText | Laptop 14\" — in stock", snapshot, StringComparison.Ordinal);
            Assert.Contains("ListItem [list 1] | Credit limit: ✓", snapshot, StringComparison.Ordinal);
            Assert.DoesNotContain("{{", snapshot, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
