// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Core;

/// <summary>
/// The document format of a template, as detected by <see cref="TemplateProcessor.DetectFormat(Stream)"/>.
/// </summary>
public enum TemplateFormat
{
    /// <summary>
    /// Not a supported template format: not a ZIP package, or a package that is neither a Word document nor an
    /// OpenDocument Text document (for example a spreadsheet, a legacy binary .doc, or flat OpenDocument .fodt).
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A Word (Office Open XML WordprocessingML) document: .docx, and also .docm, .dotx and .dotm packages.
    /// Processed by <see cref="DocumentTemplateProcessor"/>.
    /// </summary>
    Docx = 1,

    /// <summary>
    /// An OpenDocument Text document (.odt) or template (.ott), as written by LibreOffice, Collabora Online or
    /// OpenOffice. Processed by <see cref="OdtTemplateProcessor"/>.
    /// </summary>
    Odt = 2,
}
