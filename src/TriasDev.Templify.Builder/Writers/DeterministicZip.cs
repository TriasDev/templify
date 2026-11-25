// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;

namespace TriasDev.Templify.Builder.Writers;

/// <summary>One entry of a package.</summary>
/// <param name="Name">The entry name.</param>
/// <param name="Content">The entry bytes.</param>
/// <param name="Stored">Whether the entry is stored uncompressed (the ODF <c>mimetype</c> entry).</param>
internal sealed record PackagePart(string Name, byte[] Content, bool Stored = false);

/// <summary>
/// Writes a ZIP package whose bytes depend only on its parts: entries in the given order, a fixed timestamp and
/// fixed attributes.
/// </summary>
/// <remarks>
/// The bytes are reproducible for the same runtime and operating system family. <see cref="ZipArchive"/> records the
/// host platform in the "version made by" field, so the same template is not byte-identical across Windows and Unix.
/// </remarks>
internal static class DeterministicZip
{
    /// <summary>The timestamp of every entry: 2000-01-01 00:00 (DOS time has no time zone).</summary>
    private static readonly DateTimeOffset _entryTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static byte[] Create(IEnumerable<PackagePart> parts)
    {
        using MemoryStream buffer = new MemoryStream();
        using (ZipArchive archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (PackagePart part in parts)
            {
                ZipArchiveEntry entry = archive.CreateEntry(
                    part.Name,
                    part.Stored ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                entry.LastWriteTime = _entryTime;
                entry.ExternalAttributes = 0;
                using Stream stream = entry.Open();
                stream.Write(part.Content);
            }
        }

        return buffer.ToArray();
    }
}
