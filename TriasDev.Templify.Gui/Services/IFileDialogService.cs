using System.Threading.Tasks;

namespace TriasDev.Templify.Gui.Services;

/// <summary>
/// Service for file dialog operations.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Opens a file picker for template files (.docx).
    /// </summary>
    /// <returns>Selected file path, or null if cancelled.</returns>
    Task<string?> OpenTemplateFileAsync();

    /// <summary>
    /// Opens a file picker for JSON data files (.json).
    /// </summary>
    /// <returns>Selected file path, or null if cancelled.</returns>
    Task<string?> OpenJsonFileAsync();

    /// <summary>
    /// Opens a save file dialog for output files (.docx).
    /// </summary>
    /// <param name="defaultName">Default filename.</param>
    /// <returns>Selected file path, or null if cancelled.</returns>
    Task<string?> SaveOutputFileAsync(string defaultName);
}
