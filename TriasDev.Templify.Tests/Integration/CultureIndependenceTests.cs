// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Verifies that conditions, keywords and date formatting behave identically regardless of
/// the current culture (and, where possible, the machine time zone). See issue #141.
/// </summary>
public sealed class CultureIndependenceTests
{
    public static TheoryData<string> Cultures => new TheoryData<string> { "en-US", "de-DE", "tr-TR", "ar-SA" };

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ProcessTemplate_DecimalLessThan_IsCultureIndependent(string culture)
    {
        using CultureScope scope = new CultureScope(culture);

        string text = ProcessSingleConditional("{{#if Price < 100}}", new Dictionary<string, object> { ["Price"] = 99.99m });

        Assert.Equal("Match", text);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ProcessTemplate_DecimalGreaterThan_IsCultureIndependent(string culture)
    {
        using CultureScope scope = new CultureScope(culture);

        string text = ProcessSingleConditional("{{#if Price > 100}}", new Dictionary<string, object> { ["Price"] = 100.5m });

        Assert.Equal("Match", text);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ProcessTemplate_DecimalLiteralComparison_IsCultureIndependent(string culture)
    {
        using CultureScope scope = new CultureScope(culture);

        string greater = ProcessSingleConditional("{{#if Count > 1.5}}", new Dictionary<string, object> { ["Count"] = 2 });
        string less = ProcessSingleConditional("{{#if Rate <= 0.25}}", new Dictionary<string, object> { ["Rate"] = 0.2m });

        Assert.Equal("Match", greater);
        Assert.Equal("Match", less);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Evaluate_GreaterThan_DoubleValue_IsCultureIndependent(string culture)
    {
        using CultureScope scope = new CultureScope(culture);

        bool result = new ConditionEvaluator().Evaluate("Price > 1", new Dictionary<string, object> { ["Price"] = 1.5 });

        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Evaluate_GreaterThan_DecimalLiteral_IsCultureIndependent(string culture)
    {
        using CultureScope scope = new CultureScope(culture);

        bool result = new ConditionEvaluator().Evaluate("Count > 1.5", new Dictionary<string, object> { ["Count"] = 2 });

        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Evaluate_NumericValueComparedWithNumericString_IsCultureIndependent(string culture)
    {
        using CultureScope scope = new CultureScope(culture);
        ConditionEvaluator evaluator = new ConditionEvaluator();

        Assert.True(evaluator.Evaluate("Price > \"1.25\"", new Dictionary<string, object> { ["Price"] = 1.5m }));
        Assert.True(evaluator.Evaluate("Price = \"1.5\"", new Dictionary<string, object> { ["Price"] = 1.5m }));
        Assert.True(evaluator.Evaluate("Price = 1.5", new Dictionary<string, object> { ["Price"] = "1.5" }));
        Assert.True(evaluator.Evaluate("Price in (\"1.5\", \"2.5\")", new Dictionary<string, object> { ["Price"] = 2.5 }));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ProcessTemplate_UppercaseKeywords_AreRecognized(string culture)
    {
        using CultureScope scope = new CultureScope(culture);

        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#IF Flag}}");
        builder.AddParagraph("{{#FOREACH Items}}");
        builder.AddParagraph("Item {{.}}");
        builder.AddParagraph("{{/FOREACH}}");
        builder.AddParagraph("{{#ELSEIF Other}}");
        builder.AddParagraph("Other");
        builder.AddParagraph("{{#ELSE}}");
        builder.AddParagraph("None");
        builder.AddParagraph("{{/IF}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Flag"] = true,
            ["Other"] = false,
            ["Items"] = new List<string> { "A", "B" }
        };

        List<string> texts = Process(builder, data);

        Assert.Equal(new[] { "Item A", "Item B" }, texts);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ProcessTemplate_UppercaseNamedForeach_IsRecognized(string culture)
    {
        using CultureScope scope = new CultureScope(culture);

        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#FOREACH item IN Items}}");
        builder.AddParagraph("{{item}}");
        builder.AddParagraph("{{/FOREACH}}");

        List<string> texts = Process(builder, new Dictionary<string, object> { ["Items"] = new List<string> { "I1", "I2" } });

        Assert.Equal(new[] { "I1", "I2" }, texts);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ProcessTemplate_DefaultDateTimeWithDateFormat_DoesNotFailDocument(string culture)
    {
        using CultureScope scope = new CultureScope(culture);

        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Born: {{BirthDate:date:dd.MM.yyyy}}");
        builder.AddParagraph("Name: {{Name}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["BirthDate"] = default(DateTime),
            ["Name"] = "Alice"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();
        ProcessingResult result = processor.ProcessTemplate(builder.ToStream(), outputStream, data);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.StartsWith("Born: ", verifier.GetParagraphText(0));
        Assert.DoesNotContain("{{", verifier.GetParagraphText(0));
        Assert.Equal("Name: Alice", verifier.GetParagraphText(1));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ConvertToString_DateTimeMinAndMaxValueWithFormat_DoesNotThrow(string culture)
    {
        using CultureScope scope = new CultureScope(culture);
        CultureInfo formatCulture = CultureInfo.CurrentCulture;

        string min = ValueConverter.ConvertToString(DateTime.MinValue, formatCulture, "date:yyyy-MM-dd", null);
        string max = ValueConverter.ConvertToString(DateTime.MaxValue, formatCulture, "date:yyyy-MM-dd", null);
        string minDefault = ValueConverter.ConvertToString(DateTime.MinValue, formatCulture);

        Assert.False(string.IsNullOrEmpty(min));
        Assert.False(string.IsNullOrEmpty(max));
        Assert.False(string.IsNullOrEmpty(minDefault));
    }

    [Fact]
    public void ConvertToString_DateTimeMinValueWithFormat_KeepsCalendarDate()
    {
        string result = ValueConverter.ConvertToString(DateTime.MinValue, CultureInfo.InvariantCulture, "date:dd.MM.yyyy", null);

        Assert.Equal("01.01.0001", result);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void WarningReport_GeneratedAt_UsesInvariantGregorianFormat(string culture)
    {
        using CultureScope scope = new CultureScope(culture);

        Dictionary<string, object> data = WarningReportGenerator.BuildReportData(
            Array.Empty<ProcessingWarning>(),
            new DateTime(2026, 9, 25, 14, 30, 0));

        Assert.Equal("2026-09-25 14:30:00", data["GeneratedAt"]);
    }

    private static string ProcessSingleConditional(string ifMarker, Dictionary<string, object> data)
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph(ifMarker);
        builder.AddParagraph("Match");
        builder.AddParagraph("{{#else}}");
        builder.AddParagraph("NoMatch");
        builder.AddParagraph("{{/if}}");

        List<string> texts = Process(builder, data);
        return Assert.Single(texts);
    }

    private static List<string> Process(DocumentBuilder builder, Dictionary<string, object> data)
    {
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        ProcessingResult result = processor.ProcessTemplate(builder.ToStream(), outputStream, data);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        return verifier.GetAllParagraphTexts();
    }

    /// <summary>Temporarily switches CurrentCulture and CurrentUICulture, restoring them on dispose.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUICulture;

        public CultureScope(string name)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalUICulture = CultureInfo.CurrentUICulture;
            CultureInfo culture = new CultureInfo(name);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUICulture;
        }
    }
}
