// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder.Json;

/// <summary>What a JSON import may use.</summary>
public sealed class TemplateJsonImportOptions
{
    /// <summary>Gets the images and embedded fonts the JSON refers to (keys as in <see cref="TemplateJsonDocument.Assets"/>).</summary>
    public IReadOnlyDictionary<string, byte[]>? Assets { get; init; }

    /// <summary>Gets the data the JSON may read: every path outside the catalogue is an error (TB9203). Null for no check.</summary>
    public DataCatalog? Data { get; init; }

    /// <summary>Gets the components the JSON may refer to by name.</summary>
    public ComponentCatalog? Components { get; init; }

    /// <summary>
    /// Gets value format names the host registered with Templify (for example a boolean formatter "janein") that a
    /// definition may use besides the built-in ones; any other unknown format is an error.
    /// </summary>
    public IReadOnlyCollection<string>? ValueFormats { get; init; }

    /// <summary>Gets a value indicating whether raw XML blocks are accepted. Default: false (a JSON definition is input from outside).</summary>
    public bool AllowRawXml { get; init; }
}
