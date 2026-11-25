// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// The <c>w:updateFields</c> setting written by <see cref="PlaceholderReplacementOptions.UpdateFieldsOnOpen"/> must
/// sit at its schema position in <c>CT_Settings</c> (a sequence), so the output validates whenever the template does
/// (#254).
/// </summary>
public sealed class UpdateFieldsOnOpenSchemaTests
{
    [Theory]
    [InlineData(UpdateFieldsOnOpenMode.Always)]
    [InlineData(UpdateFieldsOnOpenMode.Auto)]
    public void ProcessTemplate_SettingsWithChildren_OutputIsSchemaValid(UpdateFieldsOnOpenMode mode)
    {
        MemoryStream template = BuildTemplate(settings: CreateNonTrivialSettings());
        AssertSchemaValid(template, "template");

        MemoryStream output = Process(template, mode);

        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Empty(verifier.GetValidationErrors());
        Assert.True(verifier.HasUpdateFieldsOnOpen());
        Settings settings = verifier.Document.MainDocumentPart!.DocumentSettingsPart!.Settings!;
        Assert.Single(settings.Elements<UpdateFieldsOnOpen>());
        // updateFields comes after characterSpacingControl and before compat in CT_Settings.
        List<string> names = settings.ChildElements.Select(e => e.LocalName).ToList();
        Assert.True(names.IndexOf("characterSpacingControl") < names.IndexOf("updateFields"));
        Assert.True(names.IndexOf("updateFields") < names.IndexOf("compat"));
    }

    [Fact]
    public void ProcessTemplate_ExistingUpdateFieldsFalse_IsUpdatedInPlace()
    {
        Settings settings = CreateNonTrivialSettings();
        settings.InsertBefore(new UpdateFieldsOnOpen { Val = false }, settings.GetFirstChild<Compatibility>());
        MemoryStream template = BuildTemplate(settings);
        AssertSchemaValid(template, "template");

        MemoryStream output = Process(template, UpdateFieldsOnOpenMode.Always);

        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Empty(verifier.GetValidationErrors());
        Assert.True(verifier.HasUpdateFieldsOnOpen());
        Assert.Single(verifier.Document.MainDocumentPart!.DocumentSettingsPart!.Settings!.Elements<UpdateFieldsOnOpen>());
    }

    [Fact]
    public void ProcessTemplate_SettingsWithUnknownAndMisorderedChildren_StillSetsUpdateFields()
    {
        // Settings the SDK cannot place a child into by schema must not make processing fail.
        Settings settings = new Settings(
            new Compatibility(),
            new Zoom { Percent = "100" },
            new DocumentFormat.OpenXml.OpenXmlUnknownElement("x", "custom", "urn:example:custom"));
        MemoryStream template = BuildTemplate(settings);

        MemoryStream output = Process(template, UpdateFieldsOnOpenMode.Always);

        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.True(verifier.HasUpdateFieldsOnOpen());
        Assert.Single(verifier.Document.MainDocumentPart!.DocumentSettingsPart!.Settings!.Elements<UpdateFieldsOnOpen>());
    }

    [Fact]
    public void ProcessTemplate_NoSettingsPart_OutputIsSchemaValid()
    {
        MemoryStream template = BuildTemplate(settings: null);

        MemoryStream output = Process(template, UpdateFieldsOnOpenMode.Always);

        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Empty(verifier.GetValidationErrors());
        Assert.True(verifier.HasUpdateFieldsOnOpen());
    }

    /// <summary>
    /// A settings part like Word writes it: children before and after <c>w:updateFields</c> in schema order.
    /// </summary>
    private static Settings CreateNonTrivialSettings()
    {
        return new Settings(
            new Zoom { Percent = "100" },
            new DisplayBackgroundShape(),
            new DefaultTabStop { Val = 708 },
            new CharacterSpacingControl { Val = CharacterSpacingValues.DoNotCompress },
            new Compatibility(),
            new ThemeFontLanguages { Val = "de-DE" },
            new DecimalSymbol { Val = "," },
            new ListSeparator { Val = ";" });
    }

    private static MemoryStream BuildTemplate(Settings? settings)
    {
        DocumentBuilder builder = new DocumentBuilder();
        if (settings != null)
        {
            DocumentSettingsPart settingsPart = builder.MainPart.AddNewPart<DocumentSettingsPart>();
            settingsPart.Settings = settings;
            settingsPart.Settings.Save();
        }

        // A TOC field, so Auto mode also writes the setting.
        builder.AddTableOfContents(("Chapter 1", 1));
        builder.AddHeading("Chapter 1");
        builder.AddParagraph("Content for {{Title}}");
        return builder.ToStream();
    }

    private static MemoryStream Process(MemoryStream template, UpdateFieldsOnOpenMode mode)
    {
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            UpdateFieldsOnOpen = mode,
        };
        Dictionary<string, object> data = new Dictionary<string, object> { ["Title"] = "Test" };
        MemoryStream output = new MemoryStream();

        template.Position = 0;
        ProcessingResult result = new DocumentTemplateProcessor(options).ProcessTemplate(template, output, data);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        return output;
    }

    private static void AssertSchemaValid(MemoryStream stream, string what)
    {
        using DocumentVerifier verifier = new DocumentVerifier(stream);
        List<string> errors = verifier.GetValidationErrors();
        Assert.True(errors.Count == 0, $"The {what} must be schema-valid: {string.Join("; ", errors)}");
    }
}
