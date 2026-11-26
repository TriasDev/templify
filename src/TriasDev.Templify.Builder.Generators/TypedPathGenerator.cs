// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TriasDev.Templify.Builder.Generators;

/// <summary>
/// Generates typed data paths: for every data type used in <c>TemplateComposer.DataAs&lt;T&gt;()</c> (or named in
/// <c>[assembly: TemplateData(typeof(T))]</c>) and every type reachable from it, C# 14 extension properties on
/// <c>TemplatePath&lt;T&gt;</c> and <c>LoopItem&lt;T&gt;</c>, so a definition writes <c>order.Number</c> instead of
/// <c>order.Path(o =&gt; o.Number)</c>. Each property calls that lambda API, so names follow the same rules.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TypedPathGenerator : IIncrementalGenerator
{
    private const string BuilderNamespace = "TriasDev.Templify.Builder";
    private const string PathParameter = "__templifyPath";
    private const string ItemParameter = "__templifyItem";

    private static readonly DiagnosticDescriptor _collision = new DiagnosticDescriptor(
        "TBG001",
        "Data property not generated",
        "'{0}.{1}' is not generated for {2}: it already has a member '{1}'; write the lambda, for example .Path(x => x.{1})",
        "TemplifyBuilder",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _languageVersion = new DiagnosticDescriptor(
        "TBG002",
        "Typed data paths need C# 14",
        "The typed data paths of TriasDev.Templify.Builder are C# 14 extension properties; set LangVersion to 14 or later (.NET 10 SDK or later)",
        "TemplifyBuilder",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat _typeFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<RootModel> calls = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => IsDataAsCall(node), static (syntax, token) => FromCall(syntax, token))
            .Where(static root => root is not null)
            .Select(static (root, _) => root!);

        IncrementalValueProvider<RootModel?> attributes = context.CompilationProvider.Select(static (compilation, token) => FromAttributes(compilation, token));

        // Typed paths a referenced assembly generated and lets this one use (InternalsVisibleTo): not generated again,
        // or both extension members would be in scope and every use ambiguous.
        IncrementalValueProvider<EquatableArray<string>> covered = context.CompilationProvider.Select(static (compilation, _) => Covered(compilation));

        IncrementalValueProvider<bool> supported = context.ParseOptionsProvider.Select(static (options, _) =>
            options is CSharpParseOptions csharp && csharp.LanguageVersion >= LanguageVersion.CSharp14);

        context.RegisterSourceOutput(calls.Collect().Combine(attributes).Combine(supported).Combine(covered), static (output, input) =>
        {
            (((ImmutableArray<RootModel> fromCalls, RootModel? fromAttributes), bool languageSupported), EquatableArray<string> coveredBlocks) = input;
            List<RootModel> roots = fromCalls.ToList();
            if (fromAttributes is not null)
            {
                roots.Add(fromAttributes);
            }

            List<DataTypeModel> types = roots.SelectMany(r => r.Types)
                .GroupBy(t => t.TypeName)
                .Select(g => g.First() with { IsItem = g.Any(t => t.IsItem) })
                .Where(t => t.Properties.Items.Length > 0)
                .OrderBy(t => t.TypeName, System.StringComparer.Ordinal)
                .ToList();
            if (types.Count == 0)
            {
                return;
            }

            if (!languageSupported)
            {
                output.ReportDiagnostic(Diagnostic.Create(_languageVersion, Location.None));
                return;
            }

            foreach (CollisionModel collision in roots.SelectMany(r => r.Collisions).Distinct())
            {
                output.ReportDiagnostic(Diagnostic.Create(_collision, collision.Location?.ToLocation() ?? Location.None, collision.TypeName, collision.Property, collision.Receiver));
            }

            string? source = Emit(types, new HashSet<string>(coveredBlocks, System.StringComparer.Ordinal));
            if (source is not null)
            {
                output.AddSource("TemplifyDataPaths.g.cs", source);
            }
        });
    }

    private static bool IsDataAsCall(SyntaxNode node) => node is InvocationExpressionSyntax invocation && invocation.Expression switch
    {
        MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "DataAs" } } => true,
        GenericNameSyntax { Identifier.ValueText: "DataAs" } => true,
        _ => false,
    };

    private static RootModel? FromCall(GeneratorSyntaxContext context, CancellationToken token)
    {
        if (context.SemanticModel.GetSymbolInfo(context.Node, token).Symbol is not IMethodSymbol { Name: "DataAs", TypeArguments.Length: 1 } method
            || method.ContainingType.ToDisplayString() != BuilderNamespace + ".TemplateComposer")
        {
            return null;
        }

        return Build(context.SemanticModel.Compilation, [method.TypeArguments[0]], token);
    }

    private static EquatableArray<string> Covered(Compilation compilation)
    {
        List<string> keys = new List<string>();
        foreach (IAssemblySymbol assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (!assembly.GivesAccessTo(compilation.Assembly))
            {
                continue;
            }

            foreach (AttributeData attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == BuilderNamespace + ".GeneratedDataPathsAttribute"
                    && attribute.ConstructorArguments.Length == 2
                    && attribute.ConstructorArguments[0].Value is ITypeSymbol type
                    && attribute.ConstructorArguments[1].Value is bool loopItem)
                {
                    keys.Add(BlockKey(type.ToDisplayString(_typeFormat), loopItem));
                }
            }
        }

        return new EquatableArray<string>(keys.ToImmutableArray());
    }

    private static string BlockKey(string typeName, bool loopItem) => (loopItem ? "loop:" : "path:") + typeName;

    private static RootModel? FromAttributes(Compilation compilation, CancellationToken token)
    {
        List<ITypeSymbol> roots = compilation.Assembly.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == BuilderNamespace + ".TemplateDataAttribute")
            .Select(a => a.ConstructorArguments.Length == 1 ? a.ConstructorArguments[0].Value as ITypeSymbol : null)
            .OfType<ITypeSymbol>()
            .ToList();
        return roots.Count == 0 ? null : Build(compilation, roots, token);
    }

    /// <summary>Every object type reachable from the roots, with its properties (the <c>DataCatalog</c> type rules).</summary>
    private static RootModel? Build(Compilation compilation, IReadOnlyList<ITypeSymbol> roots, CancellationToken token)
    {
        INamedTypeSymbol? path = compilation.GetTypeByMetadataName(BuilderNamespace + ".TemplatePath`1");
        INamedTypeSymbol? loopItem = compilation.GetTypeByMetadataName(BuilderNamespace + ".LoopItem`1");
        if (path is null || loopItem is null)
        {
            return null;
        }

        HashSet<string> pathMembers = MemberNames(path);
        HashSet<string> loopMembers = MemberNames(loopItem);
        List<DataTypeModel> types = new List<DataTypeModel>();
        List<CollisionModel> collisions = new List<CollisionModel>();
        HashSet<ITypeSymbol> seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        HashSet<ITypeSymbol> items = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        Queue<ITypeSymbol> pending = new Queue<ITypeSymbol>(roots);
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            ITypeSymbol type = pending.Dequeue();
            // Types of .NET itself are not walked: the lambda API rejects their members as data (PathExpression).
            if (!seen.Add(type) || Classify(compilation, type) != Shape.Object || !IsAccessible(compilation, type) || IsSystem(type) || IsObsolete(type))
            {
                continue;
            }

            string typeName = type.ToDisplayString(_typeFormat);
            List<PropertyModel> properties = new List<PropertyModel>();
            foreach (IPropertySymbol property in DataProperties(type))
            {
                if (property.ReturnsByRef || property.ReturnsByRefReadonly || property.Type.IsRefLikeType || IsObsolete(property.Type))
                {
                    continue;
                }

                ITypeSymbol bare = Unwrap(property.Type);
                PropertyKind kind = PropertyKind.Path;
                ITypeSymbol target = property.Type;
                Shape shape = Classify(compilation, bare);
                if (shape == Shape.Collection && ItemType(bare) is { } item)
                {
                    kind = PropertyKind.Collection;
                    target = item;
                    items.Add(item);
                    pending.Enqueue(item);
                }
                else if (shape == Shape.Object)
                {
                    pending.Enqueue(property.Type);
                }

                if (!IsAccessible(compilation, target) || target.IsRefLikeType || IsObsolete(target))
                {
                    continue;
                }

                bool skipOnPath = pathMembers.Contains(property.Name);
                bool skipOnLoopItem = loopMembers.Contains(property.Name);
                LocationModel? location = Where(property);
                if (skipOnPath)
                {
                    collisions.Add(new CollisionModel(type.Name, property.Name, "TemplatePath<" + type.Name + ">", location));
                }

                if (skipOnLoopItem)
                {
                    collisions.Add(new CollisionModel(type.Name, property.Name, "LoopItem<" + type.Name + ">", location));
                }

                properties.Add(new PropertyModel(property.Name, Identifier(property.Name), kind, target.ToDisplayString(_typeFormat), skipOnPath, skipOnLoopItem, ObsoleteAttribute(property)));
            }

            types.Add(new DataTypeModel(typeName, false, new EquatableArray<PropertyModel>(properties.ToImmutableArray())));
        }

        // LoopItem<T> exists only for the items of a collection: only they get loop blocks and loop collisions.
        types = types.Select(t => t with { IsItem = items.Any(i => i.ToDisplayString(_typeFormat) == t.TypeName) }).ToList();
        collisions = collisions.Where(c => c.Receiver.StartsWith("TemplatePath<", System.StringComparison.Ordinal)
            || types.Any(t => t.IsItem && t.TypeName.EndsWith("." + c.TypeName, System.StringComparison.Ordinal) && c.Receiver == "LoopItem<" + c.TypeName + ">")).ToList();

        return new RootModel(new EquatableArray<DataTypeModel>(types.ToImmutableArray()), new EquatableArray<CollisionModel>(collisions.ToImmutableArray()));
    }

    private static bool IsSystem(ITypeSymbol type)
    {
        string? name = type.ContainingNamespace?.ToDisplayString();
        return name is not null && (name == "System" || name.StartsWith("System.", System.StringComparison.Ordinal));
    }

    private static bool IsObsolete(ISymbol symbol) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.ObsoleteAttribute");

    /// <summary>An obsolete data property stays obsolete: the attribute is copied, so its use warns where it is used, not in the generated file.</summary>
    private static string? ObsoleteAttribute(IPropertySymbol property)
    {
        AttributeData? obsolete = property.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.ObsoleteAttribute");
        if (obsolete is null)
        {
            return null;
        }

        IEnumerable<string> arguments = obsolete.ConstructorArguments.Select(a => a.Value switch
        {
            string text => SymbolDisplay.FormatLiteral(text, quote: true),
            bool flag => flag ? "true" : "false",
            _ => "null",
        });
        return "[global::System.Obsolete(" + string.Join(", ", arguments) + ")]";
    }

    /// <summary>
    /// Public readable instance properties, the type's own first, then inherited ones it does not hide. For an interface,
    /// a name that only several base interfaces declare is ambiguous in C# and left out.
    /// </summary>
    private static IEnumerable<IPropertySymbol> DataProperties(ITypeSymbol type)
    {
        HashSet<string> names = new HashSet<string>(System.StringComparer.Ordinal);
        if (type.TypeKind == TypeKind.Interface)
        {
            HashSet<string> own = new HashSet<string>(type.GetMembers().OfType<IPropertySymbol>().Select(p => p.Name), System.StringComparer.Ordinal);
            foreach (IGrouping<string, INamedTypeSymbol> shared in type.AllInterfaces
                .SelectMany(i => i.GetMembers().OfType<IPropertySymbol>().Select(p => (p.Name, Owner: i)))
                .GroupBy(p => p.Name, p => p.Owner)
                .Where(g => !own.Contains(g.Key) && g.Distinct(SymbolEqualityComparer.Default).Count() > 1))
            {
                names.Add(shared.Key);
            }
        }

        IEnumerable<ITypeSymbol> chain = type.TypeKind == TypeKind.Interface
            ? new[] { type }.Concat(type.AllInterfaces)
            : Bases(type);
        foreach (ITypeSymbol owner in chain)
        {
            foreach (IPropertySymbol property in owner.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.DeclaredAccessibility != Accessibility.Public || property.IsStatic || property.IsIndexer
                    || property.GetMethod is not { DeclaredAccessibility: Accessibility.Public } || IsIgnored(property) || !names.Add(property.Name))
                {
                    continue;
                }

                yield return property;
            }
        }
    }

    private static IEnumerable<ITypeSymbol> Bases(ITypeSymbol type)
    {
        for (ITypeSymbol? current = type; current is not null && current.SpecialType != SpecialType.System_Object && current.SpecialType != SpecialType.System_ValueType; current = current.BaseType)
        {
            yield return current;
        }
    }

    /// <summary><c>[JsonIgnore]</c> without a condition, or with <c>Condition = Always</c>, hides a property.</summary>
    private static bool IsIgnored(IPropertySymbol property) => property.GetAttributes().Any(a =>
        a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonIgnoreAttribute"
        && a.NamedArguments.Where(n => n.Key == "Condition").Select(n => n.Value.Value as int?).DefaultIfEmpty(1).First() == 1);

    private enum Shape
    {
        Value,
        Open,
        Collection,
        Object,
        Other,
    }

    private static Shape Classify(Compilation compilation, ITypeSymbol type)
    {
        type = Unwrap(type);
        if (IsValue(type))
        {
            return Shape.Value;
        }

        if (IsOpen(compilation, type))
        {
            return Shape.Open;
        }

        if (ItemType(type) is not null)
        {
            return Shape.Collection;
        }

        return type.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface && type is INamedTypeSymbol ? Shape.Object : Shape.Other;
    }

    private static ITypeSymbol Unwrap(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable ? nullable.TypeArguments[0] : type;

    private static bool IsValue(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
            case SpecialType.System_String:
            case SpecialType.System_IntPtr:
            case SpecialType.System_UIntPtr:
            case SpecialType.System_DateTime:
                return true;
        }

        return type.ToDisplayString() is "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly" or "System.TimeSpan" or "System.Guid";
    }

    private static bool IsOpen(Compilation compilation, ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Dynamic)
        {
            return true;
        }

        string name = type.ToDisplayString();
        if (name is "System.Text.Json.JsonElement" or "System.Dynamic.ExpandoObject")
        {
            return true;
        }

        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "System.Text.Json.Nodes.JsonNode")
            {
                return true;
            }
        }

        return new[] { type }.Concat(type.AllInterfaces).Any(i =>
            i.ToDisplayString() == "System.Collections.IDictionary"
            || (i is INamedTypeSymbol { IsGenericType: true } generic
                && generic.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.IDictionary<TKey, TValue>" or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"));
    }

    private static ITypeSymbol? ItemType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return array.ElementType;
        }

        return new[] { type }.Concat(type.AllInterfaces)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            ?.TypeArguments[0];
    }

    /// <summary>Whether the generated code (in the compilation's assembly) can name the type.</summary>
    private static bool IsAccessible(Compilation compilation, ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => IsAccessible(compilation, array.ElementType),
        INamedTypeSymbol named => !named.IsFileLocal && compilation.IsSymbolAccessibleWithin(named.OriginalDefinition, compilation.Assembly)
            && (named.ContainingType is null || IsAccessible(compilation, named.ContainingType))
            && named.TypeArguments.All(t => IsAccessible(compilation, t)),
        ITypeParameterSymbol => false,
        _ => type.TypeKind != TypeKind.Error && type.TypeKind != TypeKind.Pointer && type.TypeKind != TypeKind.FunctionPointer,
    };

    private static HashSet<string> MemberNames(INamedTypeSymbol type)
    {
        HashSet<string> names = new HashSet<string>(type.GetMembers().Select(m => m.Name), System.StringComparer.Ordinal)
        {
            "Equals", "GetHashCode", "ToString", "GetType", "MemberwiseClone", "ReferenceEquals",
        };
        return names;
    }

    private static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    private static LocationModel? Where(IPropertySymbol property) =>
        property.Locations.FirstOrDefault(l => l.IsInSource) is { SourceTree: { } tree } location
            ? new LocationModel(tree.FilePath, location.SourceSpan, location.GetLineSpan().Span)
            : null;

    private static string? Emit(IReadOnlyList<DataTypeModel> types, HashSet<string> covered)
    {
        StringBuilder blocks = new StringBuilder();
        List<string> marks = new List<string>();
        foreach (DataTypeModel type in types)
        {
            if (!covered.Contains(BlockKey(type.TypeName, false))
                && Block(blocks, type, "global::" + BuilderNamespace + ".TemplatePath<" + type.TypeName + ">", PathParameter, p => !p.SkipOnPath))
            {
                marks.Add("[assembly: global::" + BuilderNamespace + ".GeneratedDataPaths(typeof(" + type.TypeName + "), false)]");
            }

            if (type.IsItem && !covered.Contains(BlockKey(type.TypeName, true))
                && Block(blocks, type, "global::" + BuilderNamespace + ".LoopItem<" + type.TypeName + ">", ItemParameter, p => !p.SkipOnLoopItem))
            {
                marks.Add("[assembly: global::" + BuilderNamespace + ".GeneratedDataPaths(typeof(" + type.TypeName + "), true)]");
            }
        }

        if (marks.Count == 0)
        {
            return null;
        }

        StringBuilder text = new StringBuilder();
        text.AppendLine("// <auto-generated/>");
        text.AppendLine("// Typed data paths of TriasDev.Templify.Builder: order.Number instead of order.Path(o => o.Number).");
        text.AppendLine("#nullable disable");
        text.AppendLine();
        foreach (string mark in marks)
        {
            text.AppendLine(mark);
        }

        text.AppendLine();
        text.AppendLine("namespace " + BuilderNamespace);
        text.AppendLine("{");
        text.AppendLine("    internal static class TemplifyDataPaths");
        text.AppendLine("    {");
        text.Append(blocks.ToString().TrimEnd('\n', '\r'));
        text.AppendLine();
        text.AppendLine("    }");
        text.AppendLine("}");
        return text.ToString();
    }

    private static bool Block(StringBuilder text, DataTypeModel type, string receiver, string parameter, System.Func<PropertyModel, bool> include)
    {
        List<PropertyModel> properties = type.Properties.Where(include).ToList();
        if (properties.Count == 0)
        {
            return false;
        }

        if (text.Length > 0)
        {
            text.AppendLine();
        }

        text.AppendLine("        extension(" + receiver + " " + parameter + ")");
        text.AppendLine("        {");
        foreach (PropertyModel property in properties)
        {
            if (property.Obsolete is { } obsolete)
            {
                text.AppendLine("            " + obsolete);
            }

            text.AppendLine(property.Kind == PropertyKind.Collection
                ? "            public global::" + BuilderNamespace + ".TemplateCollection<" + property.TypeName + "> " + property.Identifier + " => " + parameter + ".Items<" + property.TypeName + ">(__x => __x." + property.Identifier + ");"
                : "            public global::" + BuilderNamespace + ".TemplatePath<" + property.TypeName + "> " + property.Identifier + " => " + parameter + ".Path(__x => __x." + property.Identifier + ");");
        }

        text.AppendLine("        }");
        return true;
    }
}
