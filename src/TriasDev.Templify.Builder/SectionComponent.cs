// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace TriasDev.Templify.Builder;

/// <summary>A named section (<see cref="BlockComposer.Section"/>): its blocks, under a name the JSON form keeps.</summary>
internal sealed partial class SectionComponent(string name, Action<BlockComposer> content) : ITemplateComponent
{
    public string Name { get; } = name;

    public void Compose(BlockComposer c) => content(c);

    public static string ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!NamePattern().IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not a section name: use letters, digits, '_', '.' or '-'.", nameof(name));
        }

        return name;
    }

    public static bool IsValidName(string name) => NamePattern().IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex NamePattern();
}
