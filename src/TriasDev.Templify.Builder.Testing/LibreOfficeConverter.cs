// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace TriasDev.Templify.Builder.Testing;

/// <summary>
/// Converts documents with a local LibreOffice (headless <c>soffice</c>), for round-trip tests: a template or a
/// filled document must open and convert. Meant for local runs; check <see cref="IsAvailable"/> and skip otherwise.
/// </summary>
/// <remarks>
/// LibreOffice is looked up in <c>TEMPLIFY_SOFFICE</c> (a path), then in the default install locations of macOS,
/// Windows and Linux. Every conversion uses its own temporary user profile, so conversions can run in parallel.
/// </remarks>
public static class LibreOfficeConverter
{
    private static readonly Lazy<string?> _executable = new Lazy<string?>(FindExecutable);

    /// <summary>Gets the path of <c>soffice</c>, or null when LibreOffice was not found.</summary>
    public static string? Executable => _executable.Value;

    /// <summary>Gets a value indicating whether LibreOffice was found.</summary>
    public static bool IsAvailable => Executable != null;

    /// <summary>Converts a document and returns the converted file.</summary>
    /// <param name="document">The document.</param>
    /// <param name="inputExtension">Its extension without the dot, e.g. <c>docx</c>.</param>
    /// <param name="filter">The target of <c>--convert-to</c>, e.g. <c>pdf</c> or <c>txt:Text (encoded):UTF8</c>.</param>
    /// <param name="outputExtension">The converted file's extension, e.g. <c>pdf</c>.</param>
    /// <param name="timeout">How long to wait; default two minutes.</param>
    /// <returns>The converted file's bytes.</returns>
    /// <exception cref="InvalidOperationException">LibreOffice is not available, timed out or did not convert the document.</exception>
    public static byte[] Convert(ReadOnlyMemory<byte> document, string inputExtension, string filter, string outputExtension, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputExtension);
        ArgumentException.ThrowIfNullOrWhiteSpace(filter);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputExtension);
        string executable = Executable
            ?? throw new InvalidOperationException("LibreOffice (soffice) was not found; set TEMPLIFY_SOFFICE to its path.");

        string directory = Path.Combine(Path.GetTempPath(), "templify-lo-" + Guid.NewGuid().ToString("N"));
        string profile = Path.Combine(directory, "profile");
        Directory.CreateDirectory(profile);
        try
        {
            string input = Path.Combine(directory, "document." + inputExtension.TrimStart('.'));
            File.WriteAllBytes(input, document.ToArray());

            ProcessStartInfo startInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = directory,
            };
            startInfo.ArgumentList.Add("-env:UserInstallation=" + new Uri(profile).AbsoluteUri);
            startInfo.ArgumentList.Add("--headless");
            startInfo.ArgumentList.Add("--norestore");
            startInfo.ArgumentList.Add("--convert-to");
            startInfo.ArgumentList.Add(filter);
            startInfo.ArgumentList.Add("--outdir");
            startInfo.ArgumentList.Add(directory);
            startInfo.ArgumentList.Add(input);

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("LibreOffice could not be started.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeout ?? TimeSpan.FromMinutes(2)))
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidOperationException("The LibreOffice conversion timed out.");
            }

            string output = Path.Combine(directory, "document." + outputExtension.TrimStart('.'));
            return File.Exists(output)
                ? File.ReadAllBytes(output)
                : throw new InvalidOperationException(
                    $"LibreOffice did not convert the document (exit code {process.ExitCode}). {stdout.Result} {stderr.Result}".Trim());
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // A lingering LibreOffice process may still hold the profile; the temp directory is cleaned up later.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Converts a built template to PDF.</summary>
    /// <param name="template">The template.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] ToPdf(GeneratedTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return Convert(template.Content, template.FileExtension, "pdf", "pdf");
    }

    /// <summary>Converts a document to PDF.</summary>
    /// <param name="document">The document.</param>
    /// <param name="extension">Its extension, e.g. <c>docx</c> or <c>odt</c>.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] ToPdf(ReadOnlyMemory<byte> document, string extension) => Convert(document, extension, "pdf", "pdf");

    /// <summary>Converts a document to plain text (UTF-8, line endings normalised to <c>\n</c>, no BOM).</summary>
    /// <param name="document">The document.</param>
    /// <param name="extension">Its extension, e.g. <c>docx</c> or <c>odt</c>.</param>
    /// <returns>The text.</returns>
    public static string ToText(ReadOnlyMemory<byte> document, string extension) =>
        Encoding.UTF8.GetString(Convert(document, extension, "txt:Text (encoded):UTF8", "txt"))
            .TrimStart('\uFEFF')
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string? FindExecutable()
    {
        string? configured = Environment.GetEnvironmentVariable("TEMPLIFY_SOFFICE");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return File.Exists(configured) ? configured : null;
        }

        string[] candidates =
        [
            "/Applications/LibreOffice.app/Contents/MacOS/soffice",
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            "/usr/bin/soffice",
            "/usr/bin/libreoffice",
            "/usr/local/bin/soffice",
            "/snap/bin/libreoffice",
        ];
        return candidates.FirstOrDefault(File.Exists);
    }
}
