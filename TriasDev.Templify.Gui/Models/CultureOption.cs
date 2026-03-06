// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace TriasDev.Templify.Gui.Models;

/// <summary>
/// Represents a culture option for the UI dropdown.
/// </summary>
public class CultureOption
{
    public string DisplayName { get; }
    public CultureInfo Culture { get; }

    public CultureOption(string displayName, CultureInfo culture)
    {
        DisplayName = displayName;
        Culture = culture;
    }

    public override string ToString() => DisplayName;
}
