// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder.Json;

/// <summary>A definition exported to JSON: the text, and the binary files it refers to by content hash.</summary>
public sealed class TemplateJsonDocument
{
    internal TemplateJsonDocument(string json, IReadOnlyDictionary<string, byte[]> assets)
    {
        Json = json;
        Assets = assets;
    }

    /// <summary>Gets the JSON text.</summary>
    public string Json { get; }

    /// <summary>
    /// Gets the images and embedded fonts the JSON refers to, keyed as in the JSON (<c>sha256:&lt;hex&gt;</c>), in key
    /// order.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> Assets { get; }
}
