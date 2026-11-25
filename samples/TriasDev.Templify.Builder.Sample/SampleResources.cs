// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder.Sample;

/// <summary>Reads the sample's embedded files (the logo and the sample data).</summary>
public static class SampleResources
{
    /// <summary>Reads an embedded resource.</summary>
    public static byte[] Read(string name)
    {
        using Stream stream = typeof(SampleResources).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The sample has no embedded resource '{name}'.");
        using MemoryStream copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>Gets the sample data as JSON.</summary>
    public static string SampleData => System.Text.Encoding.UTF8.GetString(Read("sample-data.json"));
}
