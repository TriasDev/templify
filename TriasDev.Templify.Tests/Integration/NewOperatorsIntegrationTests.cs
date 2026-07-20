// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the new condition operators (in, contains, startswith,
/// endswith, exists, is empty, is not empty) and grouping, exercised through the public
/// standalone <see cref="IConditionEvaluator"/> API and through document-level template processing.
/// </summary>
public class NewOperatorsIntegrationTests
{
    private readonly IConditionEvaluator _evaluator = new ConditionEvaluator();

    [Fact]
    public void StandaloneApi_In_ListLiteral()
        => Assert.True(_evaluator.Evaluate("Status in (\"Active\", \"Pending\")",
            new Dictionary<string, object> { ["Status"] = "Active" }));

    [Fact]
    public void StandaloneApi_Contains()
        => Assert.True(_evaluator.Evaluate("Email contains \"@trias\"",
            new Dictionary<string, object> { ["Email"] = "a@trias.dev" }));

    [Fact]
    public void StandaloneApi_Exists_And_IsEmpty()
    {
        var data = new Dictionary<string, object> { ["Name"] = "Alice" };
        Assert.True(_evaluator.Evaluate("Name exists", data));
        Assert.True(_evaluator.Evaluate("Missing is empty", data));
    }

    [Fact]
    public void StandaloneApi_Grouping_And_Precedence()
    {
        var data = new Dictionary<string, object> { ["A"] = false, ["B"] = true, ["C"] = false };
        Assert.True(_evaluator.Evaluate("A or B and C = false", data)); // and binds tighter than or
        Assert.False(_evaluator.Evaluate("(A or B) and C", data));
    }

    [Fact]
    public void StandaloneApi_NotIn()
        => Assert.True(_evaluator.Evaluate("not Role in Roles",
            new Dictionary<string, object> { ["Role"] = "Guest", ["Roles"] = new List<object> { "Admin", "User" } }));

    [Fact]
    public void ProcessTemplate_IfInOperator_ConditionTrue_KeepsContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Role in Roles}}");
        builder.AddParagraph("VISIBLE");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Role"] = "Admin",
            ["Roles"] = new List<object> { "Admin", "User" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("VISIBLE", verifier.GetParagraphText(0));
    }
}
