// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Gui.Services;

/// <summary>
/// Opens files with the operating system's default application.
/// </summary>
public interface IFileLauncher
{
    /// <summary>
    /// Opens the given file with its associated application.
    /// </summary>
    /// <param name="path">Path of the file to open.</param>
    void Open(string path);
}
