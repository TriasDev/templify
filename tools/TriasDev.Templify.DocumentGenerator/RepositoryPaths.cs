namespace TriasDev.Templify.DocumentGenerator;

/// <summary>
/// Resolves repository locations used by the generator, independent of the current working directory.
/// </summary>
public static class RepositoryPaths
{
    /// <summary>
    /// Finds the repository root (the directory containing <c>templify.sln</c> and <c>examples/</c>).
    /// Searches upwards from the current directory first, then from the application's base directory
    /// (so <c>dotnet run --project</c> works from any directory inside or outside the repository).
    /// </summary>
    public static string? FindRepositoryRoot()
    {
        return FindRepositoryRoot(Directory.GetCurrentDirectory())
            ?? FindRepositoryRoot(AppContext.BaseDirectory);
    }

    /// <summary>
    /// Finds the repository root by searching upwards from <paramref name="startDirectory"/>.
    /// </summary>
    public static string? FindRepositoryRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "templify.sln"))
                && Directory.Exists(Path.Combine(dir.FullName, "examples")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>Directory for generated template documents.</summary>
    public static string TemplatesDirectory(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "examples", "templates");

    /// <summary>Directory for processed output documents.</summary>
    public static string OutputsDirectory(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "examples", "outputs");

    /// <summary>Directory for PNG screenshots used by the MkDocs site.</summary>
    public static string ImagesDirectory(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "docs", "images", "examples");
}
