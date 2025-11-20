using System.IO.Compression;

namespace TriasDev.Templify.DocumentGenerator;

/// <summary>
/// Converts DOCX files to PNG images using Stirling-PDF API
/// </summary>
public class StirlingPdfConverter : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _stirlingUrl;
    private readonly string? _apiKey;

    public StirlingPdfConverter(string stirlingUrl, string? apiKey = null)
    {
        _stirlingUrl = stirlingUrl.TrimEnd('/');
        _apiKey = apiKey;

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)  // Generous timeout for conversions
        };

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }
    }

    /// <summary>
    /// Converts a DOCX file to PNG image
    /// </summary>
    public async Task<byte[]> ConvertDocxToPngAsync(string docxPath)
    {
        // Step 1: Convert DOCX to PDF
        var pdfBytes = await ConvertDocxToPdfAsync(docxPath);

        // Step 2: Convert PDF to PNG
        var pngBytes = await ConvertPdfToPngAsync(pdfBytes);

        return pngBytes;
    }

    /// <summary>
    /// Converts DOCX to PDF
    /// </summary>
    private async Task<byte[]> ConvertDocxToPdfAsync(string docxPath)
    {
        using var content = new MultipartFormDataContent();
        using var fileStream = File.OpenRead(docxPath);
        using var streamContent = new StreamContent(fileStream);

        content.Add(streamContent, "fileInput", Path.GetFileName(docxPath));

        var response = await _httpClient.PostAsync($"{_stirlingUrl}/api/v1/convert/file/pdf", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to convert DOCX to PDF: {response.StatusCode}. {error}");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    /// <summary>
    /// Converts PDF to PNG image (extracts first page)
    /// </summary>
    private async Task<byte[]> ConvertPdfToPngAsync(byte[] pdfBytes)
    {
        using var content = new MultipartFormDataContent();
        using var pdfContent = new ByteArrayContent(pdfBytes);

        content.Add(pdfContent, "fileInput", "document.pdf");
        content.Add(new StringContent("png"), "imageFormat");
        content.Add(new StringContent("200"), "dpi");
        content.Add(new StringContent("rgb"), "colorType");

        var response = await _httpClient.PostAsync($"{_stirlingUrl}/api/v1/convert/pdf/img", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to convert PDF to PNG: {response.StatusCode}. {error}");
        }

        var resultBytes = await response.Content.ReadAsByteArrayAsync();

        // Check if result is a ZIP file (multi-page PDF)
        if (IsZipFile(resultBytes))
        {
            return ExtractFirstPngFromZip(resultBytes);
        }

        return resultBytes;
    }

    /// <summary>
    /// Checks if the byte array is a ZIP file
    /// </summary>
    private static bool IsZipFile(byte[] bytes)
    {
        if (bytes.Length < 4) return false;
        // ZIP file magic number: 50 4B 03 04 or 50 4B 05 06
        return bytes[0] == 0x50 && bytes[1] == 0x4B &&
               (bytes[2] == 0x03 || bytes[2] == 0x05);
    }

    /// <summary>
    /// Extracts the first PNG image from a ZIP archive
    /// </summary>
    private static byte[] ExtractFirstPngFromZip(byte[] zipBytes)
    {
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var pngEntry = archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

        if (pngEntry == null)
        {
            throw new InvalidOperationException("No PNG file found in ZIP archive");
        }

        using var entryStream = pngEntry.Open();
        using var memoryStream = new MemoryStream();
        entryStream.CopyTo(memoryStream);

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Tests connectivity to Stirling-PDF instance
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(_stirlingUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
