// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// <c>ValidateTemplate(template, data)</c> warns (<see cref="ValidationWarningType.MissingConditionVariable"/>) when a
/// condition evaluates a variable that is missing from the data for truthiness on its own. Every case is validated as
/// a Word and as an OpenDocument template, directly and through the <see cref="TemplateProcessor"/> facade.
/// </summary>
public sealed class ValidationMissingConditionVariableTests
{
    private static Dictionary<string, object> Data() => new Dictionary<string, object>
    {
        ["A"] = true,
        ["Status"] = "Active",
        ["Role"] = "Admin",
        ["Global"] = "g",
        ["Items"] = new List<Dictionary<string, object>>
        {
            new() { ["Title"] = "i1", ["Active"] = true },
            new() { ["Title"] = "i2" },
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
            Assert.Equal(Describe(word.Warnings), Describe(other.Warnings));
            Assert.Equal(word.Errors.Select(e => e.Message).Order(), other.Errors.Select(e => e.Message).Order());
            Assert.Equal(word.MissingVariables, other.MissingVariables);
        }

        return word;
    }

    private static List<string> Describe(IEnumerable<ValidationWarning> warnings) =>
        warnings.Select(w => $"{w.Type}: {w.Message} @ {w.Location}").ToList();

    private static List<ValidationWarning> ConditionWarnings(ValidationResult result) =>
        result.Warnings.Where(w => w.Type == ValidationWarningType.MissingConditionVariable).ToList();

    public static TheoryData<string, string[], string> WarningCases => new TheoryData<string, string[], string>
    {
        { "Missing", new[] { "{{#if Missing}}", "x", "{{/if}}" }, "{{#if Missing}}" },
        { "Missing", new[] { "{{#if not Missing}}", "x", "{{/if}}" }, "{{#if not Missing}}" },
        { "Missing", new[] { "{{#if A and Missing}}", "x", "{{/if}}" }, "{{#if A and Missing}}" },
        { "Missing", new[] { "{{#if Missing or Status = \"Active\"}}", "x", "{{/if}}" }, "{{#if Missing or Status = \"Active\"}}" },
        { "Missing", new[] { "{{#if (A or Missing) and not A}}", "x", "{{/if}}" }, "{{#if (A or Missing) and not A}}" },
        { "Missing", new[] { "{{#if A}}", "a", "{{#elseif Missing}}", "b", "{{/if}}" }, "{{#elseif Missing}}" },
        { "Missing", new[] { "Text {{#if Missing}}x{{/if}} end" }, "{{#if Missing}}" },
        { "Customer.Name", new[] { "{{#if Customer.Name}}", "x", "{{/if}}" }, "{{#if Customer.Name}}" },
    };

    [Theory]
    [MemberData(nameof(WarningCases))]
    public void BareMissingOperand_Warns(string variable, string[] paragraphs, string location)
    {
        ValidationResult result = Validate(Data(), paragraphs);

        ValidationWarning warning = Assert.Single(ConditionWarnings(result));
        Assert.Contains($"'{variable}'", warning.Message);
        Assert.Contains($"Condition '{location[(location.IndexOf(' ') + 1)..^2]}'", warning.Message);
        Assert.Equal(location, warning.Location);

        // Warning only: no error, no missing variable, still valid.
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Empty(result.MissingVariables);
    }

    public static TheoryData<string> NoWarningConditions => new TheoryData<string>
    {
        "A",
        "not A",
        "A and Status = \"Active\"",
        "Missing exists",
        "not (Missing exists)",
        "Missing is empty",
        "Missing is not empty",
        "A and Missing is empty",
        "Status = Active",
        "Missing = \"x\"",
        "Missing != Other",
        "Role in (\"Admin\", \"Owner\")",
        "Missing in (\"Admin\", Other)",
        "Missing contains \"x\"",
        "Status startswith Missing",
        "true",
        "not null",
        "\"text\"",
        "1",
    };

    [Theory]
    [MemberData(nameof(NoWarningConditions))]
    public void NonBareOrResolvedOperand_DoesNotWarn(string condition)
    {
        ValidationResult result = Validate(Data(), $"{{{{#if {condition}}}}}", "x", "{{/if}}");

        Assert.Empty(ConditionWarnings(result));
    }

    [Fact]
    public void WithoutData_DoesNotWarn()
    {
        ValidationResult result = Validate(null, "{{#if Missing}}", "x", "{{/if}}");

        Assert.Empty(result.Warnings);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void InvalidCondition_IsAnErrorOnly()
    {
        ValidationResult result = Validate(Data(), "{{#if Missing and}}", "x", "{{/if}}");

        Assert.Empty(ConditionWarnings(result));
        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.InvalidConditionalExpression);
    }

    [Fact]
    public void SameConditionTwice_WarnsOnce()
    {
        ValidationResult result = Validate(
            Data(),
            "{{#if Missing}}", "x", "{{/if}}",
            "{{#if Missing}}", "y", "{{/if}}");

        Assert.Single(ConditionWarnings(result));
    }

    [Fact]
    public void SeveralMissingOperands_WarnEach()
    {
        ValidationResult result = Validate(Data(), "{{#if First or not Second}}", "x", "{{/if}}");

        Assert.Equal(
            new[] { "First", "Second" },
            ConditionWarnings(result).Select(w => w.Message.Contains("'First'") ? "First" : "Second").Order());
    }

    [Fact]
    public void InLoop_ItemPropertiesNamedVariablesMetadataAndGlobals_Resolve()
    {
        ValidationResult result = Validate(
            Data(),
            "{{#foreach item in Items}}",
            "{{#if item.Active}}a{{/if}} {{#if Active}}b{{/if}} {{#if item}}c{{/if}} {{#if Title and Global}}d{{/if}}",
            "{{#if @first}}e{{/if}} {{#if not @last and @index}}f{{/if}} {{#if this}}g{{/if}}",
            "{{/foreach}}");

        Assert.Empty(ConditionWarnings(result));
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void InLoop_MissingItemPropertyOrVariable_Warns()
    {
        ValidationResult result = Validate(
            Data(),
            "{{#foreach item in Items}}",
            "{{#if item.Nope}}", "a", "{{/if}}",
            "{{#if Nope}}", "b", "{{/if}}",
            "{{/foreach}}");

        Assert.Equal(
            new[] { "Condition 'Nope' uses variable 'Nope'", "Condition 'item.Nope' uses variable 'item.Nope'" },
            ConditionWarnings(result).Select(w => w.Message[..w.Message.IndexOf(", which", StringComparison.Ordinal)]).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ItemPropertyOutsideTheLoop_Warns()
    {
        ValidationResult result = Validate(
            Data(),
            "{{#foreach Items}}", "{{#if Active}}a{{/if}}", "{{/foreach}}",
            "{{#if Active}}", "b", "{{/if}}");

        Assert.Single(ConditionWarnings(result));
    }

    [Fact]
    public void InEmptyLoop_IsNotChecked()
    {
        ValidationResult result = Validate(Data(), "{{#foreach Empty}}", "{{#if Nope}}x{{/if}}", "{{/foreach}}");

        Assert.Empty(ConditionWarnings(result));
    }

    [Fact]
    public void InlineExpression_IsNotChecked()
    {
        ValidationResult result = Validate(Data(), "{{(Missing and A)}} {{(not Missing):yesno}}");

        Assert.Empty(ConditionWarnings(result));
    }

    [Fact]
    public void TableRowConditionalAndHeader_Warn()
    {
        DocumentBuilder docx = new DocumentBuilder();
        docx.AddTable(3, 1, (row, _) => row switch
        {
            0 => "{{#if RowFlag}}",
            1 => "x",
            _ => "{{/if}}"
        });
        docx.AddHeader("{{#if HeaderFlag}}h{{/if}}");

        OdtDocumentBuilder odt = new OdtDocumentBuilder();
        odt.AddTable(new[] { "{{#if RowFlag}}" }, new[] { "x" }, new[] { "{{/if}}" });
        odt.AddHeaderParagraph("{{#if HeaderFlag}}h{{/if}}");

        ValidationResult result = ValidateAll(docx.ToStream().ToArray(), odt.ToBytes(), Data());

        Assert.Equal(2, ConditionWarnings(result).Count);
        Assert.Contains(result.Warnings, w => w.Message.Contains("'RowFlag'"));
        Assert.Contains(result.Warnings, w => w.Message.Contains("'HeaderFlag'"));
    }
}
