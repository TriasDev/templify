// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Converter.Converters;

namespace TriasDev.Templify.Converter.Tests;

public class ConditionBuilderTests
{
    [Theory]
    [InlineData("field", "field")]
    [InlineData("field_not", "not field")]
    [InlineData("key_eq_other", "key = \"other\"")]
    [InlineData("key_ne_other", "key != \"other\"")]
    [InlineData("count_gt_5", "count > 5")]
    [InlineData("count_lt_5", "count < 5")]
    [InlineData("count_gte_5", "count >= 5")]
    [InlineData("count_lte_-5", "count <= -5")]
    [InlineData("key_eq_other_not", "not key = \"other\"")]
    [InlineData("A_and_B", "A and B")]
    [InlineData("X_or_Y", "X or Y")]
    [InlineData("status_eq_a_or_b", "status = \"a\" or b")]
    [InlineData("a_or_b_and_c", "(a or b) and c")]
    [InlineData("a_and_b_or_c", "a and b or c")]
    [InlineData("a_and_b_not", "not (a and b)")]
    [InlineData("a_not_and_b", "not a and b")]
    [InlineData("a_not_not", "not (not a)")]
    [InlineData("x_not_or__y", "not x or y")]
    [InlineData("is_active", "is_active")]
    [InlineData("is_active_and_has_items", "is_active and has_items")]
    [InlineData("order.total_gt_1.5", "order.total > 1.5")]
    [InlineData("items[0].name_eq_x", "items[0].name = \"x\"")]
    public void Build_ValidTags_ProducesExpectedExpression(string arguments, string expected)
    {
        ConditionBuildResult result = ConditionBuilder.Build(arguments);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal(expected, result.Expression);
        Assert.True(new ConditionEvaluator().Validate(result.Expression!).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("and_b")]
    [InlineData("a_or")]
    [InlineData("a_and")]
    [InlineData("a_eq")]
    [InlineData("a_eq_and_b")]
    [InlineData("a_or_b_eq_1")]
    [InlineData("a_not_eq_1")]
    [InlineData("a_eq_x\"y")]
    [InlineData("a_or_b-c")]
    public void Build_UnconvertibleTags_ReportsErrorInsteadOfBrokenSyntax(string arguments)
    {
        ConditionBuildResult result = ConditionBuilder.Build(arguments);

        Assert.False(result.IsValid);
        Assert.Null(result.Expression);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Build_UnderscoreNames_AreKeptAndFlaggedForReview()
    {
        ConditionBuildResult result = ConditionBuilder.Build("is_active");

        Assert.Equal("is_active", result.VariablePath);
        Assert.Contains(result.ReviewNotes, note => note.Contains("'is'"));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    [InlineData("en-US")]
    public void Build_NumericValues_AreCultureInvariant(string culture)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            Assert.Equal("price > 1.5", ConditionBuilder.Build("price_gt_1.5").Expression);
            Assert.Equal("price > \"1,5\"", ConditionBuilder.Build("price_gt_1,5").Expression);
            Assert.Equal("price > 1000", ConditionBuilder.Build("price_gt_1000").Expression);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    /// The generated expressions evaluate like OpenXMLTemplates would (left to right).
    /// </summary>
    [Theory]
    [InlineData("a_or_b_and_c", true, false, false, false)]
    [InlineData("a_or_b_and_c", true, true, true, true)]
    [InlineData("a_and_b_or_c", false, false, true, true)]
    [InlineData("a_or_b_not", false, true, false, false)]
    [InlineData("a_or_b_not", false, false, false, true)]
    [InlineData("a_not_or_b", true, false, false, false)]
    [InlineData("a_not_or_b", true, true, false, true)]
    public void Build_Expressions_FollowLeftToRightSemantics(string arguments, bool a, bool b, bool c, bool expected)
    {
        string expression = ConditionBuilder.Build(arguments).Expression!;
        Dictionary<string, object> data = new() { ["a"] = a, ["b"] = b, ["c"] = c };

        Assert.Equal(expected, new ConditionEvaluator().Evaluate(expression, data));
    }
}
