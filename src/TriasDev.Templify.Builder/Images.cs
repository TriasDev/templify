// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace TriasDev.Templify.Builder;

/// <summary>
/// An image to place in a template: PNG or JPEG bytes, for example a customer's logo passed in as a theme slot. Its
/// pixel size and resolution are read from the file.
/// </summary>
public sealed class ImageSource
{
    private ImageSource(byte[] data, bool isPng, int pixelWidth, int pixelHeight, double dpiX, double dpiY)
    {
        Data = data;
        IsPng = isPng;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        DpiX = dpiX;
        DpiY = dpiY;
        Hash = Convert.ToHexStringLower(SHA256.HashData(data));
    }

    /// <summary>Gets the width in pixels.</summary>
    public int PixelWidth { get; }

    /// <summary>Gets the height in pixels.</summary>
    public int PixelHeight { get; }

    /// <summary>Gets the width at the image's own resolution (96 dpi when the file names none).</summary>
    public Length NaturalWidth => Length.FromPoints(PixelWidth * 72.0 / DpiX);

    /// <summary>Gets the height at the image's own resolution (96 dpi when the file names none).</summary>
    public Length NaturalHeight => Length.FromPoints(PixelHeight * 72.0 / DpiY);

    internal byte[] Data { get; }

    internal bool IsPng { get; }

    internal double DpiX { get; }

    internal double DpiY { get; }

    /// <summary>The SHA-256 of the bytes: equal images are stored once.</summary>
    internal string Hash { get; }

    internal string Extension => IsPng ? "png" : "jpeg";

    internal string MediaType => IsPng ? "image/png" : "image/jpeg";

    /// <summary>A PNG image.</summary>
    /// <param name="data">The file's bytes.</param>
    /// <returns>The image.</returns>
    /// <exception cref="ArgumentException">The bytes are not a PNG image.</exception>
    public static ImageSource Png(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return TryPng(data) ?? throw new ArgumentException("The bytes are not a PNG image.", nameof(data));
    }

    /// <summary>A JPEG image.</summary>
    /// <param name="data">The file's bytes.</param>
    /// <returns>The image.</returns>
    /// <exception cref="ArgumentException">The bytes are not a JPEG image.</exception>
    public static ImageSource Jpeg(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return TryJpeg(data) ?? throw new ArgumentException("The bytes are not a JPEG image.", nameof(data));
    }

    /// <summary>A PNG or JPEG image, told apart by its signature (for uploads of unknown type).</summary>
    /// <param name="data">The file's bytes.</param>
    /// <returns>The image.</returns>
    /// <exception cref="ArgumentException">The bytes are neither a PNG nor a JPEG image.</exception>
    public static ImageSource FromBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return TryPng(data) ?? TryJpeg(data) ?? throw new ArgumentException("The bytes are neither a PNG nor a JPEG image.", nameof(data));
    }

    private static ImageSource? TryPng(byte[] data)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (data.Length < 33 || !data.AsSpan(0, 8).SequenceEqual(signature) || !data.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }

        int width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20, 4));
        double dpiX = 96;
        double dpiY = 96;

        // pHYs (pixels per metre, unit 1) before the image data.
        for (int offset = 8; offset + 12 <= data.Length;)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4));
            if (length < 0 || offset + 12 + length > data.Length)
            {
                break;
            }

            ReadOnlySpan<byte> type = data.AsSpan(offset + 4, 4);
            if (type.SequenceEqual("pHYs"u8) && length >= 9 && data[offset + 16] == 1)
            {
                int ppmX = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset + 8, 4));
                int ppmY = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset + 12, 4));
                if (ppmX > 0 && ppmY > 0)
                {
                    dpiX = ppmX * 0.0254;
                    dpiY = ppmY * 0.0254;
                }
            }

            if (type.SequenceEqual("IDAT"u8))
            {
                break;
            }

            offset += 12 + length;
        }

        return width > 0 && height > 0 ? new ImageSource(data.ToArray(), true, width, height, dpiX, dpiY) : null;
    }

    private static ImageSource? TryJpeg(byte[] data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return null;
        }

        double dpiX = 96;
        double dpiY = 96;
        int offset = 2;
        while (offset + 4 <= data.Length)
        {
            if (data[offset] != 0xFF)
            {
                return null;
            }

            byte marker = data[offset + 1];
            int length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2, 2));
            if (marker == 0xE0 && length >= 14 && offset + 16 <= data.Length && data.AsSpan(offset + 4, 5).SequenceEqual("JFIF\0"u8))
            {
                byte units = data[offset + 11];
                int densityX = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 12, 2));
                int densityY = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 14, 2));
                if (units is 1 or 2 && densityX > 0 && densityY > 0)
                {
                    dpiX = units == 1 ? densityX : densityX * 2.54;
                    dpiY = units == 1 ? densityY : densityY * 2.54;
                }
            }

            // Start of frame (baseline, progressive, …); C4, C8 and CC are not frames.
            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC) && offset + 9 <= data.Length)
            {
                int height = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 5, 2));
                int width = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 7, 2));
                return width > 0 && height > 0 ? new ImageSource(data.ToArray(), false, width, height, dpiX, dpiY) : null;
            }

            offset += 2 + length;
        }

        return null;
    }
}

/// <summary>How an image is sized: its natural size, a given width or height, or fitted into a box. The aspect ratio is always kept.</summary>
public sealed class ImageFit
{
    private readonly Length? _width;
    private readonly Length? _height;

    private ImageFit(Length? width, Length? height)
    {
        _width = width;
        _height = height;
    }

    /// <summary>Gets the image's natural size (its pixels at its own resolution).</summary>
    public static ImageFit Natural { get; } = new ImageFit(null, null);

    /// <summary>A given width; the height follows.</summary>
    /// <param name="width">The width.</param>
    /// <returns>The fit.</returns>
    public static ImageFit Width(Length width) => new ImageFit(StyleRanges.Positive(width, nameof(width)), null);

    /// <summary>A given height; the width follows.</summary>
    /// <param name="height">The height.</param>
    /// <returns>The fit.</returns>
    public static ImageFit Height(Length height) => new ImageFit(null, StyleRanges.Positive(height, nameof(height)));

    /// <summary>As large as fits into a box, for example a logo area.</summary>
    /// <param name="width">The box width.</param>
    /// <param name="height">The box height.</param>
    /// <returns>The fit.</returns>
    public static ImageFit Within(Length width, Length height) =>
        new ImageFit(StyleRanges.Positive(width, nameof(width)), StyleRanges.Positive(height, nameof(height)));

    /// <summary>The size of an image under this fit.</summary>
    internal (Length Width, Length Height) Resolve(ImageSource image)
    {
        double ratio = image.NaturalHeight.Points / image.NaturalWidth.Points;
        return (_width, _height) switch
        {
            ({ } w, { } h) when h.Points / w.Points < ratio => (Length.FromPoints(h.Points / ratio), h),
            ({ } w, { }) => (w, Length.FromPoints(w.Points * ratio)),
            ({ } w, null) => (w, Length.FromPoints(w.Points * ratio)),
            (null, { } h) => (Length.FromPoints(h.Points / ratio), h),
            _ => (image.NaturalWidth, image.NaturalHeight),
        };
    }
}
