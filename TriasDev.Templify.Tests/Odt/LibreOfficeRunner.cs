// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Runs LibreOffice headless conversions for round-trip tests. Local only: tests using it skip when
/// no <c>soffice</c> is found (set <c>TEMPLIFY_SOFFICE</c> to its path to use a specific installation).
/// </summary>
internal static class LibreOfficeRunner
{
    private static readonly Lazy<string?> _executable = new Lazy<string?>(FindExecutable);

    /// <summary>Gets the path of soffice, or null when LibreOffice is not installed.</summary>
    public static string? Executable => _executable.Value;

    /// <summary>Skips the current test when LibreOffice is not installed.</summary>
    public static string RequireExecutable()
    {
        string? executable = Executable;
        Assert.SkipWhen(executable == null, "LibreOffice (soffice) is not installed; set TEMPLIFY_SOFFICE to run round-trip tests.");
        return executable!;
    }

    /// <summary>
    /// Converts a document with LibreOffice headless and returns the converted file's bytes.
    /// </summary>
    /// <param name="document">The document to convert.</param>
    /// <param name="inputExtension">The input file extension, e.g. <c>odt</c>.</param>
    /// <param name="filter">The target of <c>--convert-to</c>, e.g. <c>txt:Text (encoded):UTF8</c> or <c>pdf</c>.</param>
    /// <param name="outputExtension">The extension of the converted file.</param>
    public static byte[] Convert(byte[] document, string inputExtension, string filter, string outputExtension)
    {
        string executable = RequireExecutable();
        string directory = Path.Combine(Path.GetTempPath(), "templify-lo-" + Guid.NewGuid().ToString("N"));
        string profile = Path.Combine(directory, "profile");
        Directory.CreateDirectory(profile);

        try
        {
            string input = Path.Combine(directory, "document." + inputExtension);
            File.WriteAllBytes(input, document);

            ProcessStartInfo startInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = directory,
            };

            // A private profile per conversion lets the test processes of all target frameworks run concurrently.
            startInfo.ArgumentList.Add("-env:UserInstallation=" + new Uri(profile).AbsoluteUri);
            startInfo.ArgumentList.Add("--headless");
            startInfo.ArgumentList.Add("--norestore");
            startInfo.ArgumentList.Add("--convert-to");
            startInfo.ArgumentList.Add(filter);
            startInfo.ArgumentList.Add("--outdir");
            startInfo.ArgumentList.Add(directory);
            startInfo.ArgumentList.Add(input);

            using Process process = Process.Start(startInfo)!;
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120_000))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("LibreOffice conversion timed out.");
            }

            string output = Path.Combine(directory, "document." + outputExtension);
            Assert.True(
                File.Exists(output),
                $"LibreOffice did not convert the document (exit code {process.ExitCode}). {stdout.Result} {stderr.Result}");
            return File.ReadAllBytes(output);
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

    /// <summary>Converts an ODT document to plain text lines (one per paragraph).</summary>
    public static string[] ConvertToTextLines(byte[] odt)
    {
        string text = Encoding.UTF8.GetString(Convert(odt, "odt", "txt:Text (encoded):UTF8", "txt"));
        return text.TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
    }

    private static string? FindExecutable()
    {
        string? configured = Environment.GetEnvironmentVariable("TEMPLIFY_SOFFICE");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return File.Exists(configured) ? configured : null;
        }

        // Round trips are a local check: CI runs them only when TEMPLIFY_SOFFICE is set explicitly.
        if (string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        List<string> candidates = new List<string>
        {
            "/Applications/LibreOffice.app/Contents/MacOS/soffice",
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            "/usr/bin/soffice",
            "/usr/bin/libreoffice",
            "/usr/local/bin/soffice",
            "/snap/bin/libreoffice",
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
