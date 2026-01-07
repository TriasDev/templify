// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Core;

/// <summary>
/// Specifies when Word should update all fields (including Table of Contents)
/// when the document is first opened after processing.
/// </summary>
public enum UpdateFieldsOnOpenMode
{
    /// <summary>
    /// Never set the UpdateFieldsOnOpen flag. This is the default for backward compatibility.
    /// </summary>
    Never,

    /// <summary>
    /// Always set the UpdateFieldsOnOpen flag, regardless of document content.
    /// Word will prompt the user to update fields every time the document is opened.
    /// </summary>
    Always,

    /// <summary>
    /// Automatically detect if the document contains fields (TOC, PAGE, NUMPAGES, etc.)
    /// and only set the UpdateFieldsOnOpen flag if fields are present.
    /// This is the recommended setting for applications that process various templates.
    /// </summary>
    Auto
}
