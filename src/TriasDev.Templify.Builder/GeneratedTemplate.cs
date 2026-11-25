// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder;

/// <summary>
/// A built template: its bytes and what describes them. Hand it to a user as a download, or fill it with
/// <see cref="TemplateProcessor"/>.
/// </summary>
public sealed class GeneratedTemplate
{
    private readonly byte[] _content;

    internal GeneratedTemplate(TemplateFormat format, byte[] content, DataContract contract, TemplateStamp stamp)
    {
        Format = format;
        _content = content;
        Contract = contract;
        Stamp = stamp;
        ContentHash = Convert.ToHexStringLower(SHA256.HashData(content));
    }

    /// <summary>Gets the file format of the template.</summary>
    public TemplateFormat Format { get; }

    /// <summary>Gets the file extension for the format, including the dot: <c>.docx</c> or <c>.odt</c>.</summary>
    public string FileExtension => Format == TemplateFormat.Docx ? ".docx" : ".odt";

    /// <summary>Gets the media type for the format, for example for an HTTP download.</summary>
    public string MediaType => Format == TemplateFormat.Docx
        ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        : "application/vnd.oasis.opendocument.text";

    /// <summary>Gets the data the template needs: every path it prints, tests or loops over.</summary>
    public DataContract Contract { get; }

    /// <summary>Gets the stamp written into the template's document properties.</summary>
    public TemplateStamp Stamp { get; }

    /// <summary>Gets the template's bytes.</summary>
    public ReadOnlyMemory<byte> Content => _content;

    /// <summary>
    /// Gets the SHA-256 of <see cref="Content"/> as lower-case hex. Builds are deterministic, so the hash can serve
    /// as a cache key or an HTTP ETag.
    /// </summary>
    public string ContentHash { get; }

    /// <summary>Writes the template's bytes to a stream.</summary>
    /// <param name="destination">The stream to write to.</param>
    public void WriteTo(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(_content);
    }

    /// <summary>Opens a new read-only, seekable stream over the template's bytes.</summary>
    /// <returns>A stream positioned at the start.</returns>
    public Stream OpenRead() => new MemoryStream(_content, writable: false);
}
