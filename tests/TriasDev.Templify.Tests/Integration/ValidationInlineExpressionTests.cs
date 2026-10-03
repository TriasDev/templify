// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// <c>ValidateTemplate</c> validates inline expression placeholders (<c>{{(A and B)}}</c>) through the parsed expression:
/// the referenced variables are listed and checked (not the expression text as one variable), and expressions that
/// cannot be parsed are errors. Every case is validated as a Word and as an OpenDocument template, directly and through
/// the <see cref="TemplateProcessor"/> facade, and the results must predict processing.
/// </summary>
public sealed class ValidationInlineExpressionTests
{
    private static Dictionary<string, object> Data() => new Dictionary<string, object>
    {
        ["A"] = true,
        ["B"] = false,
        ["IsActive"] = true,
        ["Price"] = 150m,
        ["Count"] = 3,
        ["Status"] = "Active",
        ["Role"] = "Admin",
        ["Roles"] = new List<string> { "Admin", "Owner" },
        ["Name"] = "Alice",
        ["Customer"] = new Dictionary<string, object> { ["Vip"] = true },
        ["Items"] = new List<Dictionary<string, object>>
        {
            new() { ["Title"] = "i1", ["Active"] = true, ["Qty"] = 2 },
            new() { ["Title"] = "i2", ["Active"] = false, ["Qty"] = 0 },
        },
        ["Empty"] = new List<object>(),
    };

    /// <summary>Validates the paragraphs in both formats and both entry points; returns the Word result.</summary>
    private static ValidationResult Validate(Dictionary<string, object>? data, params string[] paragraphs)
    {
        DocumentBuilder docx = new DocumentBuilder();
        OdtDocumentBuilder odt = new OdtDocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            docx.AddParagraph(paragraph);
            odt.AddParagraph(paragraph);
        }

        return ValidateAll(docx.ToStream().ToArray(), odt.ToBytes(), data);
    }

    private static ValidationResult ValidateAll(byte[] docxBytes, byte[] odtBytes, Dictionary<string, object>? data)
    {
        ValidationResult word = data == null
            ? new DocumentTemplateProcessor().ValidateTemplate(new MemoryStream(docxBytes))
            : new DocumentTemplateProcessor().ValidateTemplate(new MemoryStream(docxBytes), data);
        ValidationResult[] others = data == null
            ? new[]
            {
                new OdtTemplateProcessor().ValidateTemplate(new MemoryStream(odtBytes)),
                new TemplateProcessor().ValidateTemplate(new MemoryStream(docxBytes)),
                new TemplateProcessor().ValidateTemplate(new MemoryStream(odtBytes)),
            }
            : new[]
            {
                new OdtTemplateProcessor().ValidateTemplate(new MemoryStream(odtBytes), data),
                new TemplateProcessor().ValidateTemplate(new MemoryStream(docxBytes), data),
                new TemplateProcessor().ValidateTemplate(new MemoryStream(odtBytes), data),
            };

        foreach (ValidationResult other in others)
        {
            Assert.Equal(Describe(word.Errors), Describe(other.Errors));
            Assert.Equal(word.Warnings.Select(w => $"{w.Type}: {w.Message}"), other.Warnings.Select(w => $"{w.Type}: {w.Message}"));
            Assert.Equal(word.AllPlaceholders, other.AllPlaceholders);
            Assert.Equal(word.MissingVariables, other.MissingVariables);
            Assert.Equal(word.IsValid, other.IsValid);
        }

        return word;
    }

    private static List<string> Describe(IEnumerable<ValidationError> errors) =>
        errors.Select(e => $"{e.Type}: {e.Message} @ {e.Location}").Order(StringComparer.Ordinal).ToList();

    private static string Errors(ValidationResult result) => string.Join("; ", result.Errors.Select(e => e.Message));

    /// <summary>Processes the paragraphs as a Word document; returns the result and the paragraph texts.</summary>
    private static (ProcessingResult Result, List<string> Paragraphs) Process(Dictionary<string, object> data, params string[] paragraphs)
    {
        DocumentBuilder docx = new DocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            docx.AddParagraph(paragraph);
        }

        using MemoryStream template = docx.ToStream();
        using MemoryStream output = new MemoryStream();
        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(template, output, data);
        using DocumentVerifier verifier = new DocumentVerifier(output);
        return (result, verifier.GetAllParagraphTexts());
    }

    public static TheoryData<string> ValidExpressions => new TheoryData<string>
    {
        "{{(A and B)}}",
        "{{(A or B)}}",
        "{{(not B)}}",
        "{{(IsActive):yesno}}",
        "{{(IsActive and not B):checkbox}}",
        "{{(Price > 100)}}",
        "{{(Price >= 100 and Count < 5):yesno}}",
        "{{((A or B) and not (Price < 5))}}",
        "{{(((A) and (not B)) or (Count = 3))}}",
        "{{(Status = 'Active')}}",
        "{{(Status = \"Active\")}}",
        "{{(Status = Active)}}",
        "{{(Status != Pending)}}",
        "{{(Missing = \"x\")}}",
        "{{(Missing > 100)}}",
        "{{(Customer.Vip and Name)}}",
        "{{(Role in (\"Admin\", \"Owner\"))}}",
        "{{(Role in Roles)}}",
        "{{(Roles contains \"Admin\")}}",
        "{{(Name startswith \"Al\")}}",
        "{{(Missing exists)}}",
        "{{(Missing is empty)}}",
        "{{(not (Missing is not empty))}}",
        "{{(true)}}",
        "{{(1 = 1)}}",
    };

    [Theory]
    [MemberData(nameof(ValidExpressions))]
    public void ExpressionWithResolvableOperands_IsValid(string paragraph)
    {
        ValidationResult result = Validate(Data(), paragraph);

        Assert.True(result.IsValid, Errors(result));
        Assert.Empty(result.MissingVariables);
        Assert.DoesNotContain(result.AllPlaceholders, p => p.StartsWith('('));

        // Validation predicts processing: the expression is evaluated, nothing is missing.
        (ProcessingResult processing, List<string> texts) = Process(Data(), paragraph);
        Assert.True(processing.IsSuccess, processing.ErrorMessage);
        Assert.Empty(processing.MissingVariables);
        Assert.Empty(processing.Warnings);
        Assert.DoesNotContain("{{", texts.Single());
    }

    [Fact]
    public void AllPlaceholders_ListsTheExpressionVariables()
    {
        ValidationResult result = Validate(
            null,
            "{{(A and B)}} {{(IsActive):yesno}} {{(Price > 100)}} {{((Customer.Vip or not Name) and Count >= 2)}}",
            "{{(Status = 'Active')}} {{(Role in (\"Admin\", Other))}} {{(true)}} {{Plain}}");

        Assert.True(result.IsValid, Errors(result));
        Assert.Equal(
            new[] { "A", "B", "Count", "Customer.Vip", "IsActive", "Name", "Other", "Plain", "Price", "Role", "Status" },
            result.AllPlaceholders);
    }

    public static TheoryData<string, string[]> MissingOperandCases => new TheoryData<string, string[]>
    {
        { "{{(A and Missing)}}", new[] { "Missing" } },
        { "{{(Missing):yesno}}", new[] { "Missing" } },
        { "{{(not Missing)}}", new[] { "Missing" } },
        { "{{((A or First) and not Second)}}", new[] { "First", "Second" } },
        { "{{((Missing) = true)}}", Array.Empty<string>() },
        { "{{((A and Missing) = true)}}", new[] { "Missing" } },
        { "{{(Customer.Nope or A)}}", new[] { "Customer.Nope" } },
        { "{{(Price > 100 and Missing)}}", new[] { "Missing" } },
        { "{{(Missing in (\"a\", \"b\"))}}", new[] { "Missing" } },
        { "{{(Role in Allowed)}}", new[] { "Allowed" } },
        { "{{(Missing contains \"x\")}}", new[] { "Missing" } },
        { "{{(Name endswith Suffix)}}", new[] { "Suffix" } },
    };

    [Theory]
    [MemberData(nameof(MissingOperandCases))]
    public void MissingOperand_IsAMissingVariableError(string paragraph, string[] missing)
    {
        ValidationResult result = Validate(Data(), paragraph);

        Assert.Equal(missing, result.MissingVariables);
        Assert.Equal(missing.Length == 0, result.IsValid);
        Assert.All(result.Errors, e => Assert.Equal(ValidationErrorType.MissingVariable, e.Type));
        Assert.Equal(
            missing.Select(m => $"Variable '{m}' is referenced in expression '{ExpressionOf(paragraph)}' but not provided in the data."),
            result.Errors.Select(e => e.Message).Order(StringComparer.Ordinal));

        // Inline expressions do not get the {{#if}} MissingConditionVariable warning: the error covers them.
        Assert.Empty(result.Warnings);
    }

    /// <summary>The expression of a single placeholder, e.g. <c>(A and B)</c> of <c>{{(A and B):yesno}}</c>.</summary>
    private static string ExpressionOf(string placeholder)
    {
        string inner = placeholder[2..^2];
        int format = inner.LastIndexOf("):", StringComparison.Ordinal);
        return format >= 0 ? inner[..(format + 1)] : inner;
    }

    [Fact]
    public void WithoutData_MissingOperandsAreNotChecked()
    {
        ValidationResult result = Validate(null, "{{(A and Missing)}} {{(Nope):yesno}}");

        Assert.True(result.IsValid, Errors(result));
        Assert.Empty(result.MissingVariables);
    }

    public static TheoryData<string, string> InvalidExpressions => new TheoryData<string, string>
    {
        { "{{(A and)}}", "(A and)" },
        { "{{(Age > 18 && Name)}}", "(Age > 18 && Name)" },
        { "{{(Status === \"Active\")}}", "(Status === \"Active\")" },
        { "{{((A or B)}}", "((A or B)" },
        { "{{( )}}", "( )" },
        { "{{(A B):yesno}}", "(A B)" },
    };

    [Theory]
    [MemberData(nameof(InvalidExpressions))]
    public void UnparsableExpression_IsAnInvalidExpressionError(string paragraph, string expression)
    {
        foreach (Dictionary<string, object>? data in new[] { null, Data() })
        {
            ValidationResult result = Validate(data, paragraph);

            ValidationError error = Assert.Single(result.Errors);
            Assert.Equal(ValidationErrorType.InvalidConditionalExpression, error.Type);
            Assert.StartsWith($"Invalid expression '{expression}': ", error.Message);
            Assert.Equal(paragraph, error.Location);
            Assert.False(result.IsValid);
            Assert.Empty(result.MissingVariables);
            Assert.DoesNotContain(result.AllPlaceholders, p => p.StartsWith('('));
        }

        // Validation predicts processing: the expression fails and is left in the document.
        (ProcessingResult processing, List<string> texts) = Process(Data(), paragraph);
        Assert.Contains(processing.Warnings, w => w.Type == ProcessingWarningType.ExpressionFailed);
        Assert.Equal(paragraph, texts.Single());
    }

    [Fact]
    public void SameInvalidExpressionTwice_IsReportedOnce()
    {
        ValidationResult result = Validate(null, "{{(A and)}}", "x {{(A and)}}");

        Assert.Single(result.Errors);
    }

    [Fact]
    public void InLoop_ItemPropertiesNamedVariablesMetadataAndGlobals_Resolve()
    {
        string[] paragraphs =
        {
            "{{#foreach item in Items}}",
            "{{(item.Active and A)}} {{(Active or not B):yesno}} {{(item.Qty > 1)}} {{(Qty = 0 and Title)}}",
            "{{(@first and not @last)}} {{(@index > 0)}} {{(item and IsActive)}}",
            "{{/foreach}}",
        };

        ValidationResult result = Validate(Data(), paragraphs);

        Assert.True(result.IsValid, Errors(result));
        Assert.Empty(result.MissingVariables);

        (ProcessingResult processing, _) = Process(Data(), paragraphs);
        Assert.Empty(processing.MissingVariables);
    }

    [Fact]
    public void InLoop_MissingItemPropertyOrVariable_IsAnError()
    {
        ValidationResult result = Validate(
            Data(),
            "{{#foreach item in Items}}",
            "{{(item.Nope and A)}} {{(Nope):yesno}} {{(item.Active)}}",
            "{{/foreach}}");

        Assert.Equal(new[] { "item.Nope", "Nope" }, result.MissingVariables.Order(StringComparer.OrdinalIgnoreCase));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ItemPropertyOutsideTheLoop_IsAnError()
    {
        ValidationResult result = Validate(
            Data(),
            "{{#foreach Items}}", "{{(Active)}}", "{{/foreach}}",
            "{{(Active)}}");

        Assert.Equal(new[] { "Active" }, result.MissingVariables);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void InEmptyLoop_IsNotChecked()
    {
        ValidationResult result = Validate(Data(), "{{#foreach Empty}}", "{{(Nope and A)}}", "{{/foreach}}");

        Assert.True(result.IsValid, Errors(result));
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void InsideConditionalBranches_IsChecked()
    {
        ValidationResult result = Validate(
            Data(),
            "{{#if A}}{{(IsActive):yesno}}{{#else}}{{(Nope)}}{{/if}}",
            "{{#if B}}",
            "{{(Price > 100 and Count = 3)}}",
            "{{/if}}");

        Assert.Equal(new[] { "Nope" }, result.MissingVariables);
    }

    [Fact]
    public void TableCellAndHeader_AreChecked()
    {
        DocumentBuilder docx = new DocumentBuilder();
        docx.AddTable(1, 1, (_, _) => "{{(A and CellMissing)}} {{(A and)}}");
        docx.AddHeader("{{(IsActive and HeaderMissing):yesno}}");

        OdtDocumentBuilder odt = new OdtDocumentBuilder();
        odt.AddTable(new[] { "{{(A and CellMissing)}} {{(A and)}}" });
        odt.AddHeaderParagraph("{{(IsActive and HeaderMissing):yesno}}");

        ValidationResult result = ValidateAll(docx.ToStream().ToArray(), odt.ToBytes(), Data());

        Assert.Equal(new[] { "CellMissing", "HeaderMissing" }, result.MissingVariables);
        Assert.Single(result.Errors, e => e.Type == ValidationErrorType.InvalidConditionalExpression);
        Assert.Contains("CellMissing", result.AllPlaceholders);
        Assert.Contains("HeaderMissing", result.AllPlaceholders);
        Assert.Contains("IsActive", result.AllPlaceholders);
    }
}
