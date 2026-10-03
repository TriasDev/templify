// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace TriasDev.Templify.Gui.Services;

/// <summary>
/// Opens files via the OS shell (<see cref="ProcessStartInfo.UseShellExecute"/>).
/// </summary>
public class ShellFileLauncher : IFileLauncher
{
    /// <inheritdoc />
    public void Open(string path)
    {
        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
