// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Writes a modified copy of a document without ever putting the input at risk: the work is done on
/// a temporary file next to the output, which then atomically replaces the output. If anything fails,
/// the input and any existing output are left untouched.
/// </summary>
public static class SafeFileWriter
{
    /// <summary>
    /// Returns true if both paths point to the same file (after normalization).
    /// </summary>
    public static bool IsSamePath(string pathA, string pathB)
    {
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(pathA), Path.GetFullPath(pathB), comparison);
    }

    /// <summary>
    /// Copy <paramref name="inputPath"/> to a temporary file in the output directory, let
    /// <paramref name="modify"/> edit the temporary file, then move it over <paramref name="outputPath"/>.
    /// </summary>
    /// <typeparam name="T">The result type of the modification.</typeparam>
    /// <param name="inputPath">The source document. It is never modified directly.</param>
    /// <param name="outputPath">The destination; may be the same as <paramref name="inputPath"/>.</param>
    /// <param name="modify">Callback that edits the temporary copy (given its path).</param>
    /// <returns>The value returned by <paramref name="modify"/>.</returns>
    public static T Write<T>(string inputPath, string outputPath, Func<string, T> modify)
    {
        ArgumentNullException.ThrowIfNull(modify);

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"File not found: {inputPath}", inputPath);
        }

        string fullOutput = Path.GetFullPath(outputPath);
        string outputDirectory = Path.GetDirectoryName(fullOutput) ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException($"Output directory does not exist: {outputDirectory}");
        }

        string tempPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(fullOutput)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(inputPath, tempPath, overwrite: false);
            T result = modify(tempPath);
            File.Move(tempPath, fullOutput, overwrite: true);
            return result;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
