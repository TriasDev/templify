// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Tests documenting the behavior of Table of Contents (TOC) in processed documents.
///
/// IMPORTANT: Templify cannot update TOC page numbers. This is a fundamental limitation
/// because page numbers are calculated by Word's layout engine at render time.
///
/// When a template with TOC is processed and content is removed (e.g., conditionals
/// evaluate to false, loops produce fewer items), the TOC will display stale page numbers.
/// Users must manually update the TOC in Word after processing.
/// </summary>
public sealed class TableOfContentsTests
{
    /// <summary>
    /// This test documents that TOC page numbers become STALE after template processing
    /// when content is removed via conditionals. This is EXPECTED BEHAVIOR.
    ///
    /// Scenario:
    /// - Template has TOC showing: Chapter 1 (page 1), Chapter 2 (page 3), Chapter 3 (page 5)
    /// - A conditional removes Chapter 2 entirely
    /// - After processing, the TOC STILL shows pages 1, 3, 5 (unchanged/stale)
    /// - The actual content has shifted, but TOC page numbers are NOT updated
    ///
    /// This is a fundamental limitation: page numbers are calculated by Word's layout
    /// engine at render time. Templify cannot know what page content will appear on.
    ///
    /// Users must manually update the TOC in Word after processing (Ctrl+A, F9).
    /// </summary>
    [Fact]
    public void ProcessTemplate_WithTOC_PageNumbersRemainStaleAfterContentRemoval()
    {
        // Arrange: Create a document with TOC and conditional content
        DocumentBuilder builder = new DocumentBuilder();

        // Add TOC with original page numbers
        builder.AddTableOfContents(
            ("Chapter 1: Introduction", 1),
            ("Chapter 2: Optional Section", 3),
            ("Chapter 3: Conclusion", 5)
        );

        // Chapter 1 - always present
        builder.AddHeading("Chapter 1: Introduction");
        builder.AddParagraph("This is the introduction content.");
        builder.AddPageBreak();

        // Chapter 2 - conditional, will be removed
        builder.AddParagraph("{{#if IncludeOptionalSection}}");
        builder.AddHeading("Chapter 2: Optional Section");
        builder.AddParagraph("This optional section has lots of content...");
        builder.AddParagraph("More content that takes up space...");
        builder.AddPageBreak();
        builder.AddParagraph("{{/if}}");

        // Chapter 3 - always present
        builder.AddHeading("Chapter 3: Conclusion");
        builder.AddParagraph("This is the conclusion.");

        MemoryStream templateStream = builder.ToStream();

        // Data: IncludeOptionalSection is false, so Chapter 2 will be removed
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IncludeOptionalSection"] = false
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // Verify TOC still exists
        Assert.True(verifier.HasTableOfContents());

        // Get TOC entries after processing
        List<string> tocEntries = verifier.GetTableOfContentsEntries();

        // This documents the LIMITATION: TOC entries are NOT updated
        // The TOC still contains entries for Chapter 2, even though it was removed
        // The cached page numbers in the TOC entries remain unchanged
        Assert.True(tocEntries.Count >= 3, $"Expected at least 3 TOC entries, found {tocEntries.Count}: [{string.Join(", ", tocEntries)}]");

        // Verify original TOC entries still exist with their original content
        Assert.Contains(tocEntries, e => e.Contains("Chapter 1") && e.Contains("1"));
        Assert.Contains(tocEntries, e => e.Contains("Chapter 2") && e.Contains("3"));
        Assert.Contains(tocEntries, e => e.Contains("Chapter 3") && e.Contains("5"));
    }

    /// <summary>
    /// Verifies that TOC structure is preserved after template processing.
    /// The TOC field itself should not be corrupted.
    /// </summary>
    [Fact]
    public void ProcessTemplate_WithTOC_PreservesTocStructure()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();

        builder.AddTableOfContents(
            ("Section A", 1),
            ("Section B", 2)
        );

        builder.AddHeading("Section A");
        builder.AddParagraph("Content for {{SectionName}}");
        builder.AddHeading("Section B");
        builder.AddParagraph("More content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["SectionName"] = "the first section"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // TOC should still be present
        Assert.True(verifier.HasTableOfContents());

        // TOC entries should be preserved (check that expected entries exist)
        List<string> entries = verifier.GetTableOfContentsEntries();
        Assert.True(entries.Count >= 2, $"Expected at least 2 TOC entries, found {entries.Count}");
        Assert.Contains(entries, e => e.Contains("Section A"));
        Assert.Contains(entries, e => e.Contains("Section B"));
    }

    /// <summary>
    /// USER ISSUE: When a template with TOC is processed, the TOC becomes stale.
    ///
    /// The user expects that when opening the document in Word, the TOC will be
    /// automatically updated to reflect the correct page numbers.
    ///
    /// SOLUTION: Set UpdateFieldsOnOpen = true so Word prompts to update fields on open.
    /// </summary>
    [Fact]
    public void ProcessTemplate_WithTOC_ShouldSetUpdateFieldsOnOpen_SoWordRefreshesToc()
    {
        // Arrange: Create a document with TOC and conditional content (user's scenario)
        DocumentBuilder builder = new DocumentBuilder();

        // TOC with page numbers that will become stale after processing
        builder.AddTableOfContents(
            ("Chapter 1: Introduction", 1),
            ("Chapter 2: Optional Section", 3),
            ("Chapter 3: Conclusion", 5)
        );

        // Chapter 1 - always present
        builder.AddHeading("Chapter 1: Introduction");
        builder.AddParagraph("Introduction content.");
        builder.AddPageBreak();

        // Chapter 2 - conditional, will be removed (causes page number shift)
        builder.AddParagraph("{{#if IncludeOptionalSection}}");
        builder.AddHeading("Chapter 2: Optional Section");
        builder.AddParagraph("Optional content that takes space.");
        builder.AddPageBreak();
        builder.AddParagraph("{{/if}}");

        // Chapter 3 - always present (now on earlier page after Chapter 2 removed)
        builder.AddHeading("Chapter 3: Conclusion");
        builder.AddParagraph("Conclusion content.");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IncludeOptionalSection"] = false  // Chapter 2 will be removed
        };

        // FIX: Enable UpdateFieldsOnOpen so Word refreshes TOC on open
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Always  // THE FIX: Word will prompt to update TOC
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // The document should have UpdateFieldsOnOpen enabled
        // so Word will prompt to refresh the TOC when opened
        Assert.True(verifier.HasUpdateFieldsOnOpen(),
            "Document should have UpdateFieldsOnOpen=true so Word refreshes TOC on open");
    }

    /// <summary>
    /// Documents that placeholder replacement works correctly in content
    /// that follows a TOC, without corrupting the TOC.
    /// </summary>
    [Fact]
    public void ProcessTemplate_PlaceholdersAfterTOC_ReplacesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();

        builder.AddTableOfContents(
            ("Overview", 1)
        );

        builder.AddHeading("Overview");
        builder.AddParagraph("Document created for {{CustomerName}} on {{Date}}.");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["CustomerName"] = "Acme Corp",
            ["Date"] = "2025-01-06"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // TOC preserved
        Assert.True(verifier.HasTableOfContents());

        // Placeholders replaced
        List<string> paragraphs = verifier.GetAllParagraphTexts();
        Assert.Contains(paragraphs, p => p.Contains("Acme Corp") && p.Contains("2025-01-06"));
    }

    /// <summary>
    /// Verifies that the UpdateFieldsOnOpen option sets the appropriate document setting.
    /// When enabled, Word will prompt the user to update all fields (including TOC)
    /// when the document is first opened.
    /// </summary>
    [Fact]
    public void ProcessTemplate_WithUpdateFieldsOnOpen_SetsDocumentSetting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();

        builder.AddTableOfContents(
            ("Chapter 1", 1),
            ("Chapter 2", 5)
        );

        builder.AddHeading("Chapter 1");
        builder.AddParagraph("Content for {{Title}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Title"] = "Test Document"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Always  // Enable auto-update
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // The document should have UpdateFieldsOnOpen setting enabled
        Assert.True(verifier.HasUpdateFieldsOnOpen(),
            "Expected UpdateFieldsOnOpen to be set in document settings");
    }

    /// <summary>
    /// Verifies that UpdateFieldsOnOpen is NOT set by default.
    /// This ensures backward compatibility.
    /// </summary>
    [Fact]
    public void ProcessTemplate_WithoutUpdateFieldsOnOpen_DoesNotSetDocumentSetting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();

        builder.AddTableOfContents(("Section", 1));
        builder.AddParagraph("Content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>();

        // Default options (UpdateFieldsOnOpen = Never)
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // UpdateFieldsOnOpen should NOT be set
        Assert.False(verifier.HasUpdateFieldsOnOpen(),
            "Expected UpdateFieldsOnOpen to NOT be set by default");
    }

    /// <summary>
    /// SOLUTION TEST: With UpdateFieldsOnOpen enabled, Word will automatically prompt
    /// to update the TOC when the document is opened, fixing stale page numbers.
    ///
    /// This test verifies the recommended solution for users with TOC documents.
    /// </summary>
    [Fact]
    public void ProcessTemplate_WithTOC_AndUpdateFieldsOnOpen_SolutionForStalePageNumbers()
    {
        // Arrange: Create a document with TOC and conditional content
        DocumentBuilder builder = new DocumentBuilder();

        builder.AddTableOfContents(
            ("Chapter 1: Introduction", 1),
            ("Chapter 2: Optional Section", 3),
            ("Chapter 3: Conclusion", 5)
        );

        builder.AddHeading("Chapter 1: Introduction");
        builder.AddParagraph("Introduction content.");

        builder.AddParagraph("{{#if IncludeOptionalSection}}");
        builder.AddHeading("Chapter 2: Optional Section");
        builder.AddParagraph("Optional content.");
        builder.AddParagraph("{{/if}}");

        builder.AddHeading("Chapter 3: Conclusion");
        builder.AddParagraph("Conclusion content.");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IncludeOptionalSection"] = false  // Chapter 2 will be removed
        };

        // THE SOLUTION: Enable UpdateFieldsOnOpen
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Always
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // TOC still exists
        Assert.True(verifier.HasTableOfContents());

        // UpdateFieldsOnOpen is set - Word will prompt to update TOC on open
        Assert.True(verifier.HasUpdateFieldsOnOpen(),
            "UpdateFieldsOnOpen should be enabled so Word updates TOC on open");

        // Note: The TOC entries still contain stale data in the file,
        // but Word will refresh them when the user opens the document
        // and confirms the update prompt.
    }

    /// <summary>
    /// Tests that Auto mode sets UpdateFieldsOnOpen when document contains a TOC.
    /// </summary>
    [Fact]
    public void ProcessTemplate_WithAutoMode_AndTOC_SetsUpdateFieldsOnOpen()
    {
        // Arrange: Document with TOC
        DocumentBuilder builder = new DocumentBuilder();

        builder.AddTableOfContents(
            ("Chapter 1", 1),
            ("Chapter 2", 2)
        );

        builder.AddHeading("Chapter 1");
        builder.AddParagraph("Content for {{Title}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Title"] = "Test"
        };

        // Auto mode - should detect TOC and set the flag
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Auto
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // Auto mode should have detected the TOC and set UpdateFieldsOnOpen
        Assert.True(verifier.HasUpdateFieldsOnOpen(),
            "Auto mode should set UpdateFieldsOnOpen when document contains TOC");
    }

    /// <summary>
    /// Tests that Auto mode does NOT set UpdateFieldsOnOpen when document has no fields.
    /// </summary>
    [Fact]
    public void ProcessTemplate_WithAutoMode_WithoutFields_DoesNotSetUpdateFieldsOnOpen()
    {
        // Arrange: Document without any fields (no TOC, no PAGE, etc.)
        DocumentBuilder builder = new DocumentBuilder();

        builder.AddParagraph("Hello {{Name}}!");
        builder.AddParagraph("This document has no fields.");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "World"
        };

        // Auto mode - should NOT set flag because there are no fields
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Auto
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // Auto mode should NOT have set UpdateFieldsOnOpen (no fields detected)
        Assert.False(verifier.HasUpdateFieldsOnOpen(),
            "Auto mode should NOT set UpdateFieldsOnOpen when document has no fields");
    }

    /// <summary>
    /// Documents the recommended usage: Auto mode for applications processing various templates.
    /// Users who upload templates with TOC will get the update prompt; others won't.
    /// </summary>
    [Fact]
    public void ProcessTemplate_AutoMode_RecommendedForMixedTemplates()
    {
        // This test documents the recommended approach for applications
        // that process templates uploaded by users (like the user's scenario).

        // Template WITH TOC
        DocumentBuilder builderWithToc = new DocumentBuilder();
        builderWithToc.AddTableOfContents(("Section", 1));
        builderWithToc.AddParagraph("Content");
        MemoryStream templateWithToc = builderWithToc.ToStream();

        // Template WITHOUT TOC
        DocumentBuilder builderWithoutToc = new DocumentBuilder();
        builderWithoutToc.AddParagraph("Simple {{Placeholder}}");
        MemoryStream templateWithoutToc = builderWithoutToc.ToStream();

        // Same options for both - Auto mode handles detection
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Auto
        };

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Placeholder"] = "content"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Process template WITH TOC
        MemoryStream outputWithToc = new MemoryStream();
        processor.ProcessTemplate(templateWithToc, outputWithToc, data);

        // Process template WITHOUT TOC
        MemoryStream outputWithoutToc = new MemoryStream();
        processor.ProcessTemplate(templateWithoutToc, outputWithoutToc, data);

        // Assert: Only the TOC document should have UpdateFieldsOnOpen set
        using DocumentVerifier verifierWithToc = new DocumentVerifier(outputWithToc);
        using DocumentVerifier verifierWithoutToc = new DocumentVerifier(outputWithoutToc);

        Assert.True(verifierWithToc.HasUpdateFieldsOnOpen(),
            "Document WITH TOC should have UpdateFieldsOnOpen");
        Assert.False(verifierWithoutToc.HasUpdateFieldsOnOpen(),
            "Document WITHOUT TOC should NOT have UpdateFieldsOnOpen");
    }
}
