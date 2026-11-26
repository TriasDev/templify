// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// The version of a component (<see cref="ITemplateComponent"/>), for standard blocks whose changes stored copies
/// should pick up (terms, legal notes). The stamp of every template records the versioned components it was built
/// from, and the check of stored templates reports copies built from an older version. Components without it are not
/// tracked.
/// </summary>
/// <remarks>
/// A JSON export names a versioned component by its registration (<see cref="ComponentCatalog"/>), so builds from the
/// JSON always use its current version; exporting an unregistered versioned component is an error (TB9204).
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ComponentVersionAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="ComponentVersionAttribute"/> class.</summary>
    /// <param name="version">The version, 1 or more.</param>
    /// <exception cref="ArgumentOutOfRangeException">The version is less than 1.</exception>
    public ComponentVersionAttribute(int version)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        Version = version;
    }

    /// <summary>Gets the version.</summary>
    public int Version { get; }
}

/// <summary>How the stamp names components and reads their versions.</summary>
internal static class ComponentVersions
{
    /// <summary>The component's name in the stamp: its type name without a generic suffix (<c>Card`1</c> → <c>Card</c>).</summary>
    public static string NameOf(Type type) => type.Name.Split('`')[0];

    /// <summary>The component's <see cref="ComponentVersionAttribute"/> version; null when it is not versioned.</summary>
    public static int? VersionOf(Type type) =>
        type.GetCustomAttributes(typeof(ComponentVersionAttribute), inherit: false) is [ComponentVersionAttribute attribute, ..] ? attribute.Version : null;

    /// <summary>The versioned components of a composed body, name → version.</summary>
    public static IReadOnlyDictionary<string, int> In(IEnumerable<Model.Block> blocks)
    {
        SortedDictionary<string, int> result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (Model.ComponentBlock block in Model.BlockTree.All(blocks).OfType<Model.ComponentBlock>())
        {
            Type type = block.Component.GetType();
            if (VersionOf(type) is { } version)
            {
                result[NameOf(type)] = Math.Max(result.GetValueOrDefault(NameOf(type)), version);
            }
        }

        return result;
    }
}
