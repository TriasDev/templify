// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

public sealed class DefinitionErrorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("customer.")]
    [InlineData(".name")]
    [InlineData("customer name")]
    [InlineData("customer-name")]
    [InlineData("{{name}}")]
    [InlineData("@index")]
    [InlineData("items[0][1]")]
    public void Path_InvalidSegment_ThrowsWhereItIsWritten(string relative)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => TemplatePath.Root("order")[relative]);
        Assert.Contains("not a valid data path", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Path_ThisAtTheRoot_Throws()
    {
        Assert.Throws<ArgumentException>(() => TemplatePath.Root("this.name"));
    }

    [Fact]
    public void Path_IsBuiltFromSegments()
    {
        TemplatePath path = TemplatePath.Root("order")["customer.address"]["city"];

        Assert.Equal("order.customer.address.city", path.ToString());
        Assert.Equal(TemplatePath.Root("order.customer.address.city"), path);
        Assert.True(path == TemplatePath.Root("order.customer.address.city"));
        Assert.True(path != TemplatePath.Root("order"));
        Assert.Equal("Straße_1", TemplatePath.Root("Straße_1").ToString());
    }

    [Fact]
    public void Build_ValueWithTheRootPath_FailsWithAllErrors()
    {
        TestDefinition definition = new TestDefinition(
            template => template.Content(c =>
            {
                c.Paragraph(template.Data);
                c.Paragraph(x => x.Value(default));
            }),
            new TemplateIdentity(" ", 1));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx)));

        Assert.Equal(new[] { "TB0002", "TB1001", "TB1001" }, exception.Errors.Select(e => e.Code));
        Assert.Contains("3 errors", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithoutIdentity_Fails()
    {
        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(new NoIdentity(), BuilderHarness.Options(TemplateFormat.Odt)));

        Assert.Equal("TB0001", Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void Build_UnknownFormat_Throws()
    {
        TemplateBuildOptions options = new TemplateBuildOptions { Format = TemplateFormat.Unknown, Language = CultureInfo.GetCultureInfo("en") };

        Assert.Throws<ArgumentException>(() => TemplateBuilder.Build(new TestDefinition(_ => { }), options));
    }

    [Fact]
    public void Build_InvariantLanguage_Throws()
    {
        TemplateBuildOptions options = new TemplateBuildOptions { Format = TemplateFormat.Docx, Language = CultureInfo.InvariantCulture };

        Assert.Throws<ArgumentException>(() => TemplateBuilder.Build(new TestDefinition(_ => { }), options));
    }

    [Fact]
    public void Composers_UsedAfterTheirLambda_Throw()
    {
        TemplateComposer? capturedTemplate = null;
        BlockComposer? capturedBlock = null;
        InlineComposer? capturedInline = null;
        TestDefinition definition = new TestDefinition(template =>
        {
            capturedTemplate = template;
            template.Content(c =>
            {
                capturedBlock = c;
                c.Paragraph(x => capturedInline = x);
            });
        });

        TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx));

        Assert.Throws<InvalidOperationException>(() => capturedTemplate!.Content(_ => { }));
        Assert.Throws<InvalidOperationException>(() => capturedBlock!.Paragraph("late"));
        Assert.Throws<InvalidOperationException>(() => capturedInline!.Text("late"));
    }

    [Fact]
    public void Build_EmptyDefinition_IsAValidEmptyDocument()
    {
        foreach (TemplateFormat format in BuilderHarness.Formats)
        {
            TestDefinition definition = new TestDefinition(_ => { });
            BuilderHarness.AssertInvariants(definition, BuilderHarness.Build(definition, format));
        }
    }

    private sealed class NoIdentity : ITemplateDefinition
    {
        public TemplateIdentity Identity => null!;

        public void Compose(TemplateComposer template)
        {
        }
    }

    [Fact]
    public void Diagnostic_LocationIsNullForCSharp()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(new ParagraphStyleKey("Missing", "Missing"), "x")));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx)));

        Assert.All(exception.Errors, e => Assert.Null(e.Location));
    }
}
