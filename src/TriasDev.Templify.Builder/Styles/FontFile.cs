// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TriasDev.Templify.Builder;

/// <summary>
/// A TrueType font file to embed: its bytes, family names and embedding permission, read from the file's tables
/// (OpenType specification: table directory, <c>name</c>, <c>OS/2</c>) without a font library.
/// </summary>
internal sealed class FontFile
{
    private FontFile(byte[] data, string family, IReadOnlyList<string> families)
    {
        Data = data;
        Family = family;
        Families = families;
        Hash = Convert.ToHexStringLower(SHA256.HashData(data));
    }

    /// <summary>The file's bytes (a copy: the caller's array may change).</summary>
    public byte[] Data { get; }

    /// <summary>The family name (name id 1, Windows English first).</summary>
    public string Family { get; }

    /// <summary>Every family name the file declares (name ids 1 and 16).</summary>
    public IReadOnlyList<string> Families { get; }

    /// <summary>The SHA-256 of the bytes, lower-case hex.</summary>
    public string Hash { get; }

    /// <summary>
    /// The DOCX font key, a GUID derived from the content (deterministic output), as Word writes it:
    /// <c>{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}</c>.
    /// </summary>
    public string DocxFontKey
    {
        get
        {
            string hex = Hash[..32].ToUpperInvariant();
            return $"{{{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}}}";
        }
    }

    /// <summary>
    /// The obfuscated DOCX font part (ECMA-376 part 1, 17.8.1): the first 32 bytes XOR the key, which is the font
    /// key's 16 bytes in reverse order.
    /// </summary>
    public byte[] ObfuscatedForDocx()
    {
        string hex = DocxFontKey.Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        byte[] key = new byte[16];
        for (int i = 0; i < 16; i++)
        {
            key[i] = byte.Parse(hex.AsSpan((15 - i) * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        byte[] result = (byte[])Data.Clone();
        for (int i = 0; i < 32 && i < result.Length; i++)
        {
            result[i] ^= key[i % 16];
        }

        return result;
    }

    /// <summary>Reads and checks a font file.</summary>
    /// <exception cref="ArgumentException">Not a TrueType font, unreadable tables, or embedding not permitted.</exception>
    public static FontFile Read(byte[] data, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(data, parameterName);
        ReadOnlySpan<byte> span = data;
        if (span.Length < 12)
        {
            throw new ArgumentException("The bytes are not a font file.", parameterName);
        }

        uint version = BinaryPrimitives.ReadUInt32BigEndian(span);
        switch (version)
        {
            case 0x00010000u:
            case 0x74727565u: // 'true'
                break;
            case 0x4F54544Fu: // 'OTTO'
                throw new ArgumentException("The font has PostScript (CFF) outlines; Word embeds TrueType fonts only. Use the font's TrueType (.ttf) version.", parameterName);
            case 0x74746366u: // 'ttcf'
                throw new ArgumentException("The bytes are a font collection (.ttc); pass the single font's .ttf file.", parameterName);
            case 0x774F4646u: // 'wOFF'
            case 0x774F4632u: // 'wOF2'
                throw new ArgumentException("The bytes are a web font (WOFF); pass the font's .ttf file.", parameterName);
            default:
                throw new ArgumentException("The bytes are not a TrueType font file.", parameterName);
        }

        ReadOnlySpan<byte> name = Table(span, "name", parameterName);
        ReadOnlySpan<byte> os2 = Table(span, "OS/2", parameterName);
        CheckEmbeddingPermission(os2, parameterName);

        List<(int Priority, string Name)> families = ReadFamilyNames(name, parameterName);
        if (families.Count == 0)
        {
            throw new ArgumentException("The font file declares no family name.", parameterName);
        }

        string family = families.OrderBy(f => f.Priority).First().Name;
        IReadOnlyList<string> all = families.Select(f => f.Name).Distinct(StringComparer.Ordinal).ToList();
        return new FontFile((byte[])data.Clone(), family, all);
    }

    private static ReadOnlySpan<byte> Table(ReadOnlySpan<byte> font, string tag, string parameterName)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(font[4..]);
        byte[] wanted = Encoding.ASCII.GetBytes(tag);
        for (int i = 0; i < count; i++)
        {
            int record = 12 + (i * 16);
            if (record + 16 > font.Length)
            {
                break;
            }

            if (font.Slice(record, 4).SequenceEqual(wanted))
            {
                long offset = BinaryPrimitives.ReadUInt32BigEndian(font[(record + 8)..]);
                long length = BinaryPrimitives.ReadUInt32BigEndian(font[(record + 12)..]);
                if (offset + length > font.Length)
                {
                    throw new ArgumentException($"The font's '{tag}' table lies outside the file.", parameterName);
                }

                return font.Slice((int)offset, (int)length);
            }
        }

        throw new ArgumentException($"The font file has no '{tag}' table.", parameterName);
    }

    /// <summary>
    /// <c>fsType</c> (OS/2 offset 8): 0 installable, bit 1 restricted, bit 2 preview and print, bit 3 editable, bit 9
    /// bitmap only. A template is edited after download, so it needs installable or editable embedding.
    /// </summary>
    private static void CheckEmbeddingPermission(ReadOnlySpan<byte> os2, string parameterName)
    {
        if (os2.Length < 10)
        {
            throw new ArgumentException("The font's 'OS/2' table is too short.", parameterName);
        }

        int fsType = BinaryPrimitives.ReadUInt16BigEndian(os2[8..]);
        int usage = fsType & 0x000F;
        if ((fsType & 0x0200) != 0)
        {
            throw new ArgumentException("The font's licence allows embedding bitmaps only (fsType bit 9); it cannot be embedded.", parameterName);
        }

        if (usage != 0 && (usage & 0x0008) == 0)
        {
            string reason = (usage & 0x0004) != 0
                ? "preview and print only (fsType 0x0004): a template is edited after download, so it needs editable embedding"
                : "no embedding (restricted licence, fsType 0x0002)";
            throw new ArgumentException($"The font's licence allows {reason}.", parameterName);
        }
    }

    /// <summary>Family names (ids 1 and 16) with a priority: Windows English id 1 first.</summary>
    private static List<(int Priority, string Name)> ReadFamilyNames(ReadOnlySpan<byte> table, string parameterName)
    {
        if (table.Length < 6)
        {
            throw new ArgumentException("The font's 'name' table is too short.", parameterName);
        }

        int count = BinaryPrimitives.ReadUInt16BigEndian(table[2..]);
        int storage = BinaryPrimitives.ReadUInt16BigEndian(table[4..]);
        List<(int, string)> names = new List<(int, string)>();
        for (int i = 0; i < count; i++)
        {
            int record = 6 + (i * 12);
            if (record + 12 > table.Length)
            {
                break;
            }

            int platform = BinaryPrimitives.ReadUInt16BigEndian(table[record..]);
            int encoding = BinaryPrimitives.ReadUInt16BigEndian(table[(record + 2)..]);
            int language = BinaryPrimitives.ReadUInt16BigEndian(table[(record + 4)..]);
            int nameId = BinaryPrimitives.ReadUInt16BigEndian(table[(record + 6)..]);
            int length = BinaryPrimitives.ReadUInt16BigEndian(table[(record + 8)..]);
            int offset = BinaryPrimitives.ReadUInt16BigEndian(table[(record + 10)..]);
            if (nameId is not (1 or 16) || storage + offset + length > table.Length)
            {
                continue;
            }

            ReadOnlySpan<byte> bytes = table.Slice(storage + offset, length);
            string? text = platform switch
            {
                3 when encoding is 0 or 1 or 10 => Encoding.BigEndianUnicode.GetString(bytes),
                0 => Encoding.BigEndianUnicode.GetString(bytes),
                1 when encoding == 0 => Encoding.Latin1.GetString(bytes),
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            int priority = (nameId == 1 ? 0 : 10) + (platform == 3 ? 0 : 2) + (platform == 3 && language == 0x409 ? 0 : 1);
            names.Add((priority, text.Trim()));
        }

        return names;
    }
}
