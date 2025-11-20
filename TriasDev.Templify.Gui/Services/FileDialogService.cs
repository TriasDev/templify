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
    private readonly IStorageProvider _storageProvider = storageProvider;

    /// <summary>
    /// Opens a file picker for template files (.docx).
    /// </summary>
    public async Task<string?> OpenTemplateFileAsync()
    {
        FilePickerOpenOptions options = new()
        {
            Title = "Select Template File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Word Documents")
                {
                    Patterns = new[] { "*.docx" },
                    MimeTypes = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" }
                }
            }
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
    /// Opens a save file dialog for output files (.docx).
    /// </summary>
    public async Task<string?> SaveOutputFileAsync(string defaultName)
    {
        FilePickerSaveOptions options = new()
        {
            Title = "Save Output File",
            SuggestedFileName = defaultName,
            DefaultExtension = "docx",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Word Documents")
                {
                    Patterns = new[] { "*.docx" },
                    MimeTypes = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" }
                }
            }
        };

        IStorageFile? result = await _storageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
    }
}
