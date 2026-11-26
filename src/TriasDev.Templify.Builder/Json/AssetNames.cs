// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace TriasDev.Templify.Builder.Json;

/// <summary>Names of the JSON form's assets (<c>"logo": "sha256:…"</c>).</summary>
internal static partial class AssetNames
{
    public static bool IsValid(string name) => Pattern().IsMatch(name);

    /// <summary>A name made from free text (a font family): letters and digits kept, everything else a dash.</summary>
    public static string From(string text)
    {
        string name = Regex.Replace(text, "[^A-Za-z0-9_.-]+", "-").Trim('-');
        return name.Length == 0 ? "asset" : name;
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex Pattern();
}
