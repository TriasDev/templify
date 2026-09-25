// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Tests for the consistent error model (#149): which errors throw, which become a failed result,
/// warnings for malformed conditions, and AST-based template validation.
/// </summary>
public sealed class ErrorModelTests
{
    private static ProcessingResult Process(DocumentBuilder builder, Dictionary<string, object> data, PlaceholderReplacementOptions? options = null)
    {
        using MemoryStream template = builder.ToStream();
        using MemoryStream output = new MemoryStream();
        return new DocumentTemplateProcessor(options).ProcessTemplate(template, output, data);
    }

    private static ValidationResult Validate(DocumentBuilder builder, Dictionary<string, object>? data = null)
    {
        using MemoryStream template = builder.ToStream();
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        return data == null ? processor.ValidateTemplate(template) : processor.ValidateTemplate(template, data);
    }

    #region DocumentTemplateProcessor: failure vs throw

    [Fact]
    public void Docx_ThrowException_MissingVariable_ThrowsPlainInvalidOperationException()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}");

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException
        };

        // Exact type (not a subclass) and unchanged message, so existing catch/assert code keeps working.
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Process(builder, new Dictionary<string, object>(), options));
        Assert.Equal("Missing variable or invalid expression: Name", ex.Message);
    }

    [Fact]
    public void Docx_ThrowException_SyntaxError_ReturnsFailureInsteadOfThrowing()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsActive}}");
        builder.AddParagraph("never closed");

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException
        };

        ProcessingResult result = Process(builder, new Dictionary<string, object> { ["IsActive"] = true }, options);

        Assert.False(result.IsSuccess);
        Assert.Contains("has no matching '{{/if}}'", result.ErrorMessage);
    }

    [Fact]
    public void Docx_UnmatchedLoop_ReturnsFailure()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{Name}}");

        ProcessingResult result = Process(builder, new Dictionary<string, object> { ["Items"] = new List<string> { "A" } });

        Assert.False(result.IsSuccess);
        Assert.StartsWith("Processing failed:", result.ErrorMessage);
        Assert.Contains("has no matching '{{/foreach}}'", result.ErrorMessage);
    }

    [Fact]
    public void Docx_LoopOverNonCollection_ReturnsFailure()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{.}}");
        builder.AddParagraph("{{/foreach}}");

        ProcessingResult result = Process(builder, new Dictionary<string, object> { ["Items"] = 42 });

        Assert.False(result.IsSuccess);
        Assert.Contains("is not a collection", result.ErrorMessage);
    }

    [Fact]
    public void Docx_InlineElseIfAfterElse_ReturnsFailure()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if A}}1{{#else}}2{{#elseif B}}3{{/if}}");

        ProcessingResult result = Process(builder, new Dictionary<string, object> { ["A"] = false, ["B"] = true });

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot appear after", result.ErrorMessage);
    }

    [Fact]
    public void Docx_JsonOverload_ThrowException_MissingVariable_StillThrows()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}");
        using MemoryStream template = builder.ToStream();
        using MemoryStream output = new MemoryStream();

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException
        });

        Assert.Throws<InvalidOperationException>(() => processor.ProcessTemplate(template, output, "{}"));
    }

    #endregion

    #region Warnings for malformed conditions

    [Theory]
    [InlineData("A && B")]
    [InlineData("Status eq \"x\"")]
    [InlineData("Name = \"unterminated")]
    public void Docx_MalformedBlockCondition_EmitsExpressionFailedWarning_AndEvaluatesFalse(string condition)
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if " + condition + "}}");
        builder.AddParagraph("Yes");
        builder.AddParagraph("{{#else}}");
        builder.AddParagraph("No");
        builder.AddParagraph("{{/if}}");

        using MemoryStream template = builder.ToStream();
        using MemoryStream output = new MemoryStream();
        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(
            template, output, new Dictionary<string, object> { ["A"] = true, ["B"] = true, ["Status"] = "x", ["Name"] = "unterminated" });

        Assert.True(result.IsSuccess);
        ProcessingWarning warning = Assert.Single(result.Warnings, w => w.Type == ProcessingWarningType.ExpressionFailed);
        Assert.Equal(condition, warning.VariableName);
        Assert.Equal("conditional", warning.Context);

        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Equal("No", verifier.GetParagraphText(0));
    }

    [Fact]
    public void Docx_MalformedInlineCondition_EmitsWarning()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Value: {{#if A && B}}yes{{#else}}no{{/if}}");

        using MemoryStream template = builder.ToStream();
        using MemoryStream output = new MemoryStream();
        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(
            template, output, new Dictionary<string, object> { ["A"] = true, ["B"] = true });

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Warnings, w => w.Type == ProcessingWarningType.ExpressionFailed && w.VariableName == "A && B");

        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Equal("Value: no", verifier.GetParagraphText(0));
    }

    [Fact]
    public void Docx_ValidCondition_EmitsNoWarning()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Status in (\"A\", \"B\") and Notes is not empty}}");
        builder.AddParagraph("Yes");
        builder.AddParagraph("{{/if}}");

        ProcessingResult result = Process(builder, new Dictionary<string, object> { ["Status"] = "A", ["Notes"] = "n" });

        Assert.True(result.IsSuccess);
        Assert.False(result.HasWarnings);
    }

    #endregion

    #region TextTemplateProcessor parity

    [Fact]
    public void Text_ThrowException_MissingVariable_ThrowsPlainInvalidOperationException()
    {
        TextTemplateProcessor processor = new TextTemplateProcessor(new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => processor.ProcessTemplate("Hello {{Name}}", new Dictionary<string, object>()));
        Assert.Equal("Missing variable: Name", ex.Message);
    }

    [Fact]
    public void Text_ThrowException_SyntaxError_ReturnsFailure()
    {
        TextTemplateProcessor processor = new TextTemplateProcessor(new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException
        });

        TextProcessingResult result = processor.ProcessTemplate("{{#if A}}open", new Dictionary<string, object> { ["A"] = true });

        Assert.False(result.IsSuccess);
        Assert.Contains("Unmatched", result.ErrorMessage);
    }

    [Fact]
    public void Text_LoopOverNonCollection_ReturnsFailure()
    {
        TextTemplateProcessor processor = new TextTemplateProcessor();

        TextProcessingResult result = processor.ProcessTemplate(
            "{{#foreach Items}}x{{/foreach}}", new Dictionary<string, object> { ["Items"] = 42 });

        Assert.False(result.IsSuccess);
        Assert.Contains("is not a collection", result.ErrorMessage);
    }

    [Fact]
    public void Text_MalformedCondition_EmitsExpressionFailedWarning()
    {
        TextTemplateProcessor processor = new TextTemplateProcessor();

        TextProcessingResult result = processor.ProcessTemplate(
            "{{#if A && B}}yes{{#else}}no{{/if}}", new Dictionary<string, object> { ["A"] = true, ["B"] = true });

        Assert.True(result.IsSuccess);
        Assert.Equal("no", result.ProcessedText);
        Assert.True(result.HasWarnings);
        ProcessingWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ProcessingWarningType.ExpressionFailed, warning.Type);
        Assert.Equal("A && B", warning.VariableName);
    }

    [Fact]
    public void Text_ValidCondition_HasNoWarnings()
    {
        TextProcessingResult result = new TextTemplateProcessor().ProcessTemplate(
            "{{#if A}}yes{{/if}}", new Dictionary<string, object> { ["A"] = true });

        Assert.True(result.IsSuccess);
        Assert.False(result.HasWarnings);
        Assert.Empty(result.Warnings);
    }

    #endregion

    #region ValidateTemplate

    [Fact]
    public void Validate_InvalidIfExpression_ReportsInvalidConditionalExpression()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if A && B}}");
        builder.AddParagraph("x");
        builder.AddParagraph("{{/if}}");

        ValidationResult result = Validate(builder);

        Assert.False(result.IsValid);
        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.InvalidConditionalExpression, error.Type);
        Assert.Contains("A && B", error.Message);
    }

    [Fact]
    public void Validate_InvalidElseIfExpression_ReportsError()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if A}}");
        builder.AddParagraph("x");
        builder.AddParagraph("{{#elseif Status === \"x\"}}");
        builder.AddParagraph("y");
        builder.AddParagraph("{{/if}}");

        ValidationResult result = Validate(builder);

        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.InvalidConditionalExpression && e.Message.Contains("==="));
    }

    [Fact]
    public void Validate_InvalidInlineExpression_ReportsError()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Text {{#if Name = \"open}}x{{/if}}");

        ValidationResult result = Validate(builder);

        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.InvalidConditionalExpression);
    }

    [Fact]
    public void Validate_InvalidExpressionInHeader_ReportsError()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Body");
        builder.AddHeader("{{#if A || B}}H{{/if}}");

        ValidationResult result = Validate(builder);

        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.InvalidConditionalExpression && e.Message.Contains("||"));
    }

    [Fact]
    public void Validate_UnmatchedConditional_StillTypedAsUnmatchedConditionalStart()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if A}}");
        builder.AddParagraph("x");

        ValidationResult result = Validate(builder);

        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.UnmatchedConditionalStart);
    }

    [Fact]
    public void Validate_UnmatchedLoop_StillTypedAsUnmatchedLoopStart()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("x");

        ValidationResult result = Validate(builder);

        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.UnmatchedLoopStart);
    }

    [Fact]
    public void Validate_InvalidIterationVariable_TypedAsInvalidPlaceholderSyntax()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach in in Items}}");
        builder.AddParagraph("x");
        builder.AddParagraph("{{/foreach}}");

        ValidationResult result = Validate(builder);

        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.InvalidPlaceholderSyntax && e.Message.Contains("reserved keyword"));
    }

    [Fact]
    public void Validate_Operators_AreNotReportedAsPlaceholdersOrMissing()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Status in (\"A\", \"B\") and Tags contains \"x\" and Notes is not empty and Ref exists}}");
        builder.AddParagraph("x");
        builder.AddParagraph("{{#elseif Count >= 10 or Flag = true or Other = null or Name startswith \"a\"}}");
        builder.AddParagraph("y");
        builder.AddParagraph("{{/if}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Status"] = "A",
            ["Tags"] = new List<string> { "x" },
            ["Notes"] = "n",
            ["Ref"] = 1,
            ["Count"] = 3,
            ["Flag"] = true,
            ["Other"] = "o",
            ["Name"] = "abc",
        };

        ValidationResult result = Validate(builder, data);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Empty(result.MissingVariables);
        Assert.Equal(
            new[] { "Count", "Flag", "Name", "Notes", "Other", "Ref", "Status", "Tags" },
            result.AllPlaceholders.OrderBy(p => p, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Validate_BareReservedWordVariableInData_Warns()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Exists}}");
        builder.AddParagraph("x");
        builder.AddParagraph("{{/if}}");

        ValidationResult result = Validate(builder, new Dictionary<string, object> { ["Exists"] = true });

        Assert.True(result.IsValid);
        ValidationWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ValidationWarningType.ReservedWordAsVariable, warning.Type);
        Assert.Contains("[Exists]", warning.Message);
        Assert.Contains("Exists", result.AllPlaceholders);
    }

    [Fact]
    public void Validate_BracketedReservedWord_DoesNotWarn()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if [Exists] and not [Empty]}}");
        builder.AddParagraph("x");
        builder.AddParagraph("{{/if}}");

        ValidationResult result = Validate(builder, new Dictionary<string, object> { ["Exists"] = true, ["Empty"] = false });

        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
        Assert.Contains("Exists", result.AllPlaceholders);
        Assert.Contains("Empty", result.AllPlaceholders);
    }

    [Fact]
    public void Validate_BareReservedWordWithoutData_DoesNotWarn()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Exists}}x{{/if}}");

        ValidationResult result = Validate(builder);

        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    #endregion

    #region Reserved words and escapes (processing)

    [Fact]
    public void Docx_BracketedReservedWordVariable_IsResolved()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if [Empty] = \"yes\" and [In]}}");
        builder.AddParagraph("Match");
        builder.AddParagraph("{{/if}}");

        using MemoryStream template = builder.ToStream();
        using MemoryStream output = new MemoryStream();
        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(
            template, output, new Dictionary<string, object> { ["Empty"] = "yes", ["In"] = true });

        Assert.True(result.IsSuccess);
        Assert.False(result.HasWarnings);
        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Equal("Match", verifier.GetParagraphText(0));
    }

    #endregion
}
