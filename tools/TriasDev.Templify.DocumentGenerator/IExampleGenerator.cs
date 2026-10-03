namespace TriasDev.Templify.DocumentGenerator;

/// <summary>
/// Interface for example document generators
/// </summary>
public interface IExampleGenerator
{
    /// <summary>
    /// Gets the name of the example (used for file naming)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a description of what this example demonstrates
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Generates the template document and returns the path
    /// </summary>
    /// <param name="outputDirectory">Directory where the template should be saved</param>
    /// <returns>Path to the generated template file</returns>
    string GenerateTemplate(string outputDirectory);

    /// <summary>
    /// Gets the sample data to process the template with
    /// </summary>
    /// <returns>Dictionary of data for template processing</returns>
    Dictionary<string, object> GetSampleData();

    /// <summary>
    /// Processes the template with Templify and returns the output path
    /// </summary>
    /// <param name="templatePath">Path to the template file</param>
    /// <param name="outputDirectory">Directory where the output should be saved</param>
    /// <returns>Path to the generated output file</returns>
    string ProcessTemplate(string templatePath, string outputDirectory);
}
