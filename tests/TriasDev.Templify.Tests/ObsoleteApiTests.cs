// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// Exercises API that is [Obsolete] since 1.8 (#156).
#pragma warning disable CS0618

using System.Reflection;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Visitors;

namespace TriasDev.Templify.Tests;

/// <summary>
/// The APIs deprecated in 1.8 (#156) are marked obsolete as warnings (not errors) and still behave
/// exactly like their internal replacements.
/// </summary>
public sealed class ObsoleteApiTests
{
    private const string AsyncMessage = "Synchronous operation wrapped in a Task; use Evaluate(...). Will be removed in 2.0.";

    public static TheoryData<string> PlaceholderInputs => new TheoryData<string>
    {
        "",
        "no placeholders here",
        "{{Name}}",
        "Hello {{Name}}, {{Customer.Address.City}} and {{Items[0].Title}}!",
        "{{Amount:currency}} {{Value:number:N2}} {{Date:date:yyyy-MM-dd}}",
        "{{(IsActive and Count > 0):yesno}} {{(not IsActive)}}",
        "{{@index}} {{.}} {{this}} {{Name}} {{Name}}",
        "{{ Name }} {{Name {{#if X}} {{/if}}",
    };

    [Theory]
    [MemberData(nameof(PlaceholderInputs))]
    public void PlaceholderFinder_BehavesLikeInternalScanner(string text)
    {
        PlaceholderFinder finder = new PlaceholderFinder();

        List<PlaceholderMatch> matches = finder.FindPlaceholders(text).ToList();
        List<PlaceholderToken> tokens = PlaceholderScanner.FindPlaceholders(text).ToList();

        Assert.Equal(tokens.Count, matches.Count);
        for (int i = 0; i < tokens.Count; i++)
        {
            Assert.Equal(tokens[i].FullMatch, matches[i].FullMatch);
            Assert.Equal(tokens[i].VariableName, matches[i].VariableName);
            Assert.Equal(tokens[i].Format, matches[i].Format);
            Assert.Equal(tokens[i].StartIndex, matches[i].StartIndex);
            Assert.Equal(tokens[i].Length, matches[i].Length);
            Assert.Equal(tokens[i].IsExpression, matches[i].IsExpression);
        }

        Assert.Equal(matches.Count, finder.FindPlaceholdersAsList(text).Count);
        Assert.Equal(PlaceholderScanner.IsValidPlaceholder(text), finder.IsValidPlaceholder(text));
        Assert.Equal(PlaceholderScanner.ExtractVariableName(text), finder.ExtractVariableName(text));
        Assert.Equal(PlaceholderScanner.GetUniqueVariableNames(text), finder.GetUniqueVariableNames(text));
    }

    [Fact]
    public void PlaceholderFinder_NullText_BehavesLikeEmptyText()
    {
        PlaceholderFinder finder = new PlaceholderFinder();

        Assert.Empty(finder.FindPlaceholders(null!));
        Assert.Empty(finder.GetUniqueVariableNames(null!));
        Assert.False(finder.IsValidPlaceholder(null!));
        Assert.Null(finder.ExtractVariableName(null!));
    }

    [Fact]
    public void ValidateTemplate_AllPlaceholders_IsTheDocumentedReplacementForPlaceholderFinder()
    {
        Helpers.DocumentBuilder builder = new Helpers.DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}, {{Customer.City}}");

        ValidationResult result = new DocumentTemplateProcessor().ValidateTemplate(builder.ToStream());

        Assert.Equal(
            new PlaceholderFinder().GetUniqueVariableNames("Hello {{Name}}, {{Customer.City}}").ToList(),
            result.AllPlaceholders.OrderBy(name => name).ToList());
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsSameResultAsEvaluate()
    {
        ConditionEvaluator evaluator = new ConditionEvaluator();
        Dictionary<string, object> data = new Dictionary<string, object> { ["Count"] = 5 };
        IConditionContext context = evaluator.CreateConditionContext(data);

        Assert.Equal(evaluator.Evaluate("Count > 3", data), await evaluator.EvaluateAsync("Count > 3", data, TestContext.Current.CancellationToken));
        Assert.Equal(evaluator.Evaluate("Count > 3", """{ "Count": 5 }"""), await evaluator.EvaluateAsync("Count > 3", """{ "Count": 5 }""", TestContext.Current.CancellationToken));
        Assert.Equal(context.Evaluate("Count < 3"), await context.EvaluateAsync("Count < 3", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AsyncMembers_AreObsoleteWarnings()
    {
        MethodInfo[] asyncMethods = new[] { typeof(ConditionEvaluator), typeof(IConditionEvaluator), typeof(ConditionContext), typeof(IConditionContext) }
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.Name.EndsWith("Async", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(8, asyncMethods.Length);
        foreach (MethodInfo method in asyncMethods)
        {
            ObsoleteAttribute? obsolete = method.GetCustomAttribute<ObsoleteAttribute>();
            Assert.NotNull(obsolete);
            Assert.False(obsolete.IsError);
            Assert.Equal(AsyncMessage, obsolete.Message);
        }
    }

    [Theory]
    [InlineData(typeof(PlaceholderFinder))]
    [InlineData(typeof(PlaceholderMatch))]
    [InlineData(typeof(TemplateElementType))]
    public void DeprecatedTypes_AreObsoleteWarningsWithGuidance(Type type)
    {
        ObsoleteAttribute? obsolete = type.GetCustomAttribute<ObsoleteAttribute>();

        Assert.NotNull(obsolete);
        Assert.False(obsolete.IsError);
        Assert.Contains("2.0", obsolete.Message);
    }

    [Fact]
    public void TemplateElementType_KeepsItsShippedValues()
    {
        Assert.Equal(0, (int)TemplateElementType.Conditional);
        Assert.Equal(1, (int)TemplateElementType.Loop);
        Assert.Equal(2, (int)TemplateElementType.Placeholder);
        Assert.Equal(3, (int)TemplateElementType.Paragraph);
        Assert.Equal(4, (int)TemplateElementType.Unknown);
    }
}
