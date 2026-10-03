// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace TriasDev.Templify.Gui.Services;

/// <summary>
/// Service for file dialog operations using Avalonia's StorageProvider.
/// </summary>
public class FileDialogService(IStorageProvider storageProvider) : IFileDialogService
{
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string OdtMimeType = "application/vnd.oasis.opendocument.text";
    private const string OttMimeType = "application/vnd.oasis.opendocument.text-template";

    private static readonly FilePickerFileType _wordDocuments = new("Word Documents")
    {
        Patterns = new[] { "*.docx" },
        MimeTypes = new[] { DocxMimeType }
    };

    private static readonly FilePickerFileType _openDocumentText = new("OpenDocument Text")
    {
        Patterns = new[] { "*.odt" },
        MimeTypes = new[] { OdtMimeType }
    };

    private readonly IStorageProvider _storageProvider = storageProvider;

    /// <summary>
    /// The filters of the template picker: all supported templates first, then each format.
    /// </summary>
    internal static IReadOnlyList<FilePickerFileType> TemplateFileTypes { get; } = new[]
    {
        new FilePickerFileType("Templates (Word, OpenDocument)")
        {
            Patterns = new[] { "*.docx", "*.odt", "*.ott" },
            MimeTypes = new[] { DocxMimeType, OdtMimeType, OttMimeType }
        },
        _wordDocuments,
        new FilePickerFileType("OpenDocument Text and Templates")
        {
            Patterns = new[] { "*.odt", "*.ott" },
            MimeTypes = new[] { OdtMimeType, OttMimeType }
        }
    };

    /// <summary>
    /// The default extension (without dot) and file type choices of the save dialog for a suggested file name:
    /// OpenDocument for an .odt name, otherwise Word.
    /// </summary>
    internal static (string DefaultExtension, IReadOnlyList<FilePickerFileType> Choices) GetSaveFileTypes(string defaultName)
    {
        bool isOdt = System.IO.Path.GetExtension(defaultName).Equals(".odt", System.StringComparison.OrdinalIgnoreCase);
        return isOdt ? ("odt", new[] { _openDocumentText }) : ("docx", new[] { _wordDocuments });
    }

    /// <summary>
    /// Opens a file picker for template files (.docx, .odt, .ott).
    /// </summary>
    public async Task<string?> OpenTemplateFileAsync()
    {
        FilePickerOpenOptions options = new()
        {
            Title = "Select Template File",
            AllowMultiple = false,
            FileTypeFilter = TemplateFileTypes
        };

        IReadOnlyList<IStorageFile> result = await _storageProvider.OpenFilePickerAsync(options);
        return result.FirstOrDefault()?.Path.LocalPath;
    }

    /// <summary>
    /// Opens a file picker for JSON data files (.json).
    /// </summary>
    public async Task<string?> OpenJsonFileAsync()
    {
        FilePickerOpenOptions options = new()
        {
            Title = "Select JSON Data File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON Files")
                {
                    Patterns = new[] { "*.json" },
                    MimeTypes = new[] { "application/json" }
                }
            }
        };

        IReadOnlyList<IStorageFile> result = await _storageProvider.OpenFilePickerAsync(options);
        return result.FirstOrDefault()?.Path.LocalPath;
    }

    /// <summary>
    /// Opens a save file dialog for output files (.docx, or .odt for an OpenDocument name).
    /// </summary>
    public async Task<string?> SaveOutputFileAsync(string defaultName)
    {
        (string defaultExtension, IReadOnlyList<FilePickerFileType> choices) = GetSaveFileTypes(defaultName);
        FilePickerSaveOptions options = new()
        {
            Title = "Save Output File",
            SuggestedFileName = defaultName,
            DefaultExtension = defaultExtension,
            FileTypeChoices = choices
        };

        IStorageFile? result = await _storageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
    }
}
