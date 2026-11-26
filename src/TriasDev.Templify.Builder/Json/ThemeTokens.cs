// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace TriasDev.Templify.Builder.Json;

/// <summary>Names of theme tokens (<c>"$accent"</c>).</summary>
internal static partial class ThemeTokens
{
    public static bool IsValid(string name) => Pattern().IsMatch(name);

    public static string Validate(string name, string parameter)
    {
        ArgumentNullException.ThrowIfNull(name, parameter);
        if (!IsValid(name))
        {
            throw new ArgumentException($"'{name}' is not a theme token name: a letter, then letters, digits, '_', '.' or '-'.", parameter);
        }

        return name;
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_.-]*$")]
    private static partial Regex Pattern();
}
