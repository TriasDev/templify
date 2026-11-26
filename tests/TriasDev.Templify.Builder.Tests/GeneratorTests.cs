// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TriasDev.Templify.Builder.Generators;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>The typed path source generator, run on client code through the Roslyn generator driver.</summary>
public sealed class GeneratorTests
{
    private const string Client = """
        using System.Collections.Generic;
        using System.Text.Json.Serialization;
        using TriasDev.Templify.Builder;

        namespace Client;

        public sealed record OrderData(Order Order);

        public class Base
        {
            public string Id { get; init; } = "";
        }

        public sealed class Order : Base
        {
            public string Number { get; init; } = "";
            public decimal? Discount { get; init; }
            public Customer Customer { get; init; } = new();
            public List<Line> Lines { get; init; } = [];
            public string[] Tags { get; init; } = [];
            public Dictionary<string, string> Extra { get; init; } = [];
            [JsonIgnore] public string Secret { get; init; } = "";
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Note { get; init; }
            public string @class { get; init; } = "";
            public string Exists { get; init; } = "";
            public static string Shared { get; } = "";
        }

        public sealed class Customer
        {
            public string Name { get; init; } = "";
            public Customer? Parent { get; init; }
        }

        public sealed class Line
        {
            public int Number { get; init; }
            public decimal Quantity { get; init; }
        }

        public sealed class Definition : ITemplateDefinition
        {
            public TemplateIdentity Identity { get; } = new("client", 1);

            public void Compose(TemplateComposer template)
            {
                TemplatePath<Order> order = template.DataAs<OrderData>().Order;
                template.Content(c =>
                {
                    c.Paragraph(order.Number);
                    c.Paragraph(order.Customer.Parent.Name);
                    c.ForEach(order.Lines, "line", (line, body) => body.Paragraph(line.Quantity));
                    c.If(order.Discount.Gt(0), b => b.Paragraph("discount"));
                });
                _ = template.DataAs<Hidden>();
            }

            private sealed record Hidden(string Secret);
        }
        """;

    private static readonly MetadataReference[] _references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Append(typeof(TemplateComposer).Assembly.Location)
        .Append(typeof(TriasDev.Templify.Core.TemplateFormat).Assembly.Location)
        .DistinctBy(Path.GetFileName)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToArray();

    private static (string? Source, ImmutableArray<Diagnostic> Generator, ImmutableArray<Diagnostic> Compiler) Run(LanguageVersion language, params string[] sources)
    {
        CSharpParseOptions parse = new CSharpParseOptions(language);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Client",
            sources.Select(s => CSharpSyntaxTree.ParseText(s, parse)),
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new TypedPathGenerator().AsSourceGenerator()], parseOptions: parse);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> generator, TestContext.Current.CancellationToken);
        string? source = output.SyntaxTrees.FirstOrDefault(t => t.FilePath.EndsWith("TemplifyDataPaths.g.cs", StringComparison.Ordinal))?.ToString();
        return (source, generator, output.GetDiagnostics(TestContext.Current.CancellationToken));
    }

    private static string Block(string source, string receiver)
    {
        int start = source.IndexOf("extension(global::TriasDev.Templify.Builder." + receiver + " ", StringComparison.Ordinal);
        Assert.True(start >= 0, "no block for " + receiver);
        return source[start..source.IndexOf("        }", start, StringComparison.Ordinal)];
    }

    [Fact]
    public void Generates_PathsForEveryReachableType()
    {
        (string? source, _, _) = Run(LanguageVersion.CSharp14, Client);

        Assert.NotNull(source);
        string order = Block(source, "TemplatePath<global::Client.Order>");
        Assert.Contains("public global::TriasDev.Templify.Builder.TemplatePath<string> Number => __templifyPath.Path(__x => __x.Number);", order, StringComparison.Ordinal);
        Assert.Contains("TemplatePath<decimal?> Discount", order, StringComparison.Ordinal);
        Assert.Contains("TemplatePath<global::Client.Customer> Customer", order, StringComparison.Ordinal);
        Assert.Contains("public global::TriasDev.Templify.Builder.TemplateCollection<global::Client.Line> Lines => __templifyPath.Items<global::Client.Line>(__x => __x.Lines);", order, StringComparison.Ordinal);
        Assert.Contains("TemplateCollection<string> Tags", order, StringComparison.Ordinal);
        Assert.Contains("TemplatePath<global::System.Collections.Generic.Dictionary<string, string>> Extra", order, StringComparison.Ordinal);
        Assert.Contains("TemplatePath<string> Id", order, StringComparison.Ordinal);
        Assert.Contains("TemplatePath<string> Note", order, StringComparison.Ordinal);
        Assert.Contains("TemplatePath<string> @class => __templifyPath.Path(__x => __x.@class);", order, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", order, StringComparison.Ordinal);
        Assert.DoesNotContain("Shared", order, StringComparison.Ordinal);

        Assert.Contains("TemplatePath<global::Client.Customer> Parent", Block(source, "TemplatePath<global::Client.Customer>"), StringComparison.Ordinal);
        Assert.Contains("TemplatePath<global::Client.Order> Order", Block(source, "TemplatePath<global::Client.OrderData>"), StringComparison.Ordinal);
        Assert.Contains("TemplatePath<int> Number", Block(source, "TemplatePath<global::Client.Line>"), StringComparison.Ordinal);
        Assert.Contains("TemplatePath<decimal> Quantity => __templifyItem.Path(__x => __x.Quantity);", Block(source, "LoopItem<global::Client.Line>"), StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Collisions_AreSkippedWithTBG001()
    {
        (string? source, ImmutableArray<Diagnostic> generator, _) = Run(LanguageVersion.CSharp14, Client);

        Assert.DoesNotContain(" Number ", Block(source!, "LoopItem<global::Client.Line>"), StringComparison.Ordinal);
        Assert.DoesNotContain(" Exists ", Block(source!, "TemplatePath<global::Client.Order>"), StringComparison.Ordinal);
        Assert.DoesNotContain("LoopItem<global::Client.Order>", source!, StringComparison.Ordinal);
        Assert.Equal(
            new[] { "'Line.Number' is not generated for LoopItem<Line>", "'Order.Exists' is not generated for TemplatePath<Order>" },
            generator.Where(d => d.Id == "TBG001").Select(d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)[..d.GetMessage(System.Globalization.CultureInfo.InvariantCulture).IndexOf(':', StringComparison.Ordinal)]).Order(StringComparer.Ordinal));
        Assert.All(generator, d => Assert.Equal("TBG001", d.Id));
        // The location is the data property's declaration (a file location: generators keep no syntax trees between runs).
        Assert.All(generator, d => Assert.True(d.Location.GetLineSpan().StartLinePosition.Line > 0));
    }

    [Fact]
    public void GeneratedCode_CompilesWithoutAnyWarning()
    {
        (_, _, ImmutableArray<Diagnostic> compiler) = Run(LanguageVersion.CSharp14, Client);

        Assert.Empty(compiler.Where(d => d.Severity >= DiagnosticSeverity.Warning));
    }

    [Fact]
    public void BelowCSharp14_IsTBG002_AndNothingIsGenerated()
    {
        (string? source, ImmutableArray<Diagnostic> generator, _) = Run(LanguageVersion.CSharp13, Client);

        Assert.Null(source);
        Assert.Equal("TBG002", Assert.Single(generator).Id);
    }

    [Fact]
    public void AssemblyAttribute_AddsARoot()
    {
        (string? source, _, ImmutableArray<Diagnostic> compiler) = Run(LanguageVersion.CSharp14, """
            using TriasDev.Templify.Builder;

            [assembly: TemplateData(typeof(Lib.Address))]

            namespace Lib;

            public sealed record Address(string Street, string City);

            public static class Use
            {
                public static TriasDev.Templify.Builder.TemplatePath<string> City(TriasDev.Templify.Builder.TemplatePath<Address> address) => address.City;
            }
            """);

        Assert.Contains("TemplatePath<string> Street", Block(source!, "TemplatePath<global::Lib.Address>"), StringComparison.Ordinal);
        Assert.Empty(compiler.Where(d => d.Severity >= DiagnosticSeverity.Warning));
    }

    [Fact]
    public void NoRoots_NoSource()
    {
        (string? source, ImmutableArray<Diagnostic> generator, _) = Run(LanguageVersion.CSharp14, "namespace Empty; public sealed class Nothing { }");

        Assert.Null(source);
        Assert.Empty(generator);
    }

    [Fact]
    public void Output_IsDeterministic()
    {
        Assert.Equal(Run(LanguageVersion.CSharp14, Client).Source, Run(LanguageVersion.CSharp14, Client).Source);
    }
}
