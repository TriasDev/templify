using DotNetEnv;
using TriasDev.Templify.DocumentGenerator;

Console.WriteLine("Templify Documentation Example Generator");
Console.WriteLine("=========================================");
Console.WriteLine();

// Resolve the repository root independently of the current working directory
var baseDir = RepositoryPaths.FindRepositoryRoot();
if (baseDir == null)
{
    Console.WriteLine("Error: Could not find the repository root (directory containing templify.sln and examples/).");
    return 1;
}

// Load environment variables (.env in the current directory or a parent, or next to the project)
var envPath = FindEnvFile(baseDir);
if (envPath != null)
{
    Env.Load(envPath);
}

var templatesDir = RepositoryPaths.TemplatesDirectory(baseDir);
var outputsDir = RepositoryPaths.OutputsDirectory(baseDir);
var imagesDir = RepositoryPaths.ImagesDirectory(baseDir);

// Ensure directories exist
Directory.CreateDirectory(templatesDir);
Directory.CreateDirectory(outputsDir);
Directory.CreateDirectory(Path.Combine(imagesDir, "templates"));
Directory.CreateDirectory(Path.Combine(imagesDir, "outputs"));

// Register all generators
var generators = ExampleGenerators.All;

// Parse command line arguments
var skipImages = args.Contains("--skip-images");
var requestedExample = args.FirstOrDefault(a => !a.StartsWith("--"));

var failures = 0;

// Generate documents
if (string.IsNullOrEmpty(requestedExample) || requestedExample == "all")
{
    Console.WriteLine("Generating all examples...");
    Console.WriteLine();

    foreach (var generator in generators)
    {
        if (!GenerateExample(generator, templatesDir, outputsDir))
        {
            failures++;
        }
    }

    Console.WriteLine();
    if (failures == 0)
    {
        Console.WriteLine($"✓ Generated {generators.Count} examples successfully!");
    }
    else
    {
        Console.WriteLine($"✗ {failures} of {generators.Count} examples failed.");
    }
}
else
{
    var generator = generators.FirstOrDefault(g => g.Name.Equals(requestedExample, StringComparison.OrdinalIgnoreCase));

    if (generator == null)
    {
        Console.WriteLine($"Error: Unknown example '{requestedExample}'");
        Console.WriteLine();
        Console.WriteLine("Available examples:");
        foreach (var g in generators)
        {
            Console.WriteLine($"  - {g.Name}: {g.Description}");
        }
        return 1;
    }

    var succeeded = GenerateExample(generator, templatesDir, outputsDir);

    Console.WriteLine();
    if (succeeded)
    {
        Console.WriteLine($"✓ Generated '{generator.Name}' successfully!");
    }
    else
    {
        failures++;
        Console.WriteLine($"✗ Generating '{generator.Name}' failed.");
    }
}

// Convert to images
if (!skipImages)
{
    Console.WriteLine();
    Console.WriteLine("=========================================");
    Console.WriteLine("Converting to Images");
    Console.WriteLine("=========================================");
    Console.WriteLine();

    var stirlingUrl = Environment.GetEnvironmentVariable("STIRLING_PDF_URL");
    var apiKey = Environment.GetEnvironmentVariable("STIRLING_PDF_API_KEY");

    if (string.IsNullOrEmpty(stirlingUrl))
    {
        Console.WriteLine("⚠ Skipping image conversion: STIRLING_PDF_URL not configured");
        Console.WriteLine("  To enable image generation:");
        Console.WriteLine("  1. Copy TriasDev.Templify.DocumentGenerator/.env.example to TriasDev.Templify.DocumentGenerator/.env");
        Console.WriteLine("  2. Set STIRLING_PDF_URL and STIRLING_PDF_API_KEY");
    }
    else
    {
        using var converter = new StirlingPdfConverter(stirlingUrl, apiKey);

        // Test connection
        Console.WriteLine($"Testing connection to Stirling-PDF: {stirlingUrl}");
        var isConnected = await converter.TestConnectionAsync();

        if (!isConnected)
        {
            failures++;
            Console.WriteLine("✗ Cannot connect to Stirling-PDF");
            Console.WriteLine("  Please ensure:");
            Console.WriteLine("  - Stirling-PDF is running");
            Console.WriteLine("  - URL is correct in .env file");
            Console.WriteLine("  - API key is valid (if required)");
            Console.WriteLine();
            Console.WriteLine("  Run with --skip-images to skip image generation");
        }
        else
        {
            Console.WriteLine("✓ Connected to Stirling-PDF");
            Console.WriteLine();

            failures += await ConvertDocumentsToImages(converter, templatesDir, Path.Combine(imagesDir, "templates"), "templates");
            failures += await ConvertDocumentsToImages(converter, outputsDir, Path.Combine(imagesDir, "outputs"), "outputs");
        }
    }
}
else
{
    Console.WriteLine();
    Console.WriteLine("⊘ Skipped image generation (--skip-images flag)");
}

// Summary
Console.WriteLine();
Console.WriteLine("=========================================");
Console.WriteLine("Summary");
Console.WriteLine("=========================================");
Console.WriteLine();

var templateCount = Directory.GetFiles(templatesDir, "*.docx").Length;
var outputCount = Directory.GetFiles(outputsDir, "*.docx").Length;
var templateImageCount = Directory.GetFiles(Path.Combine(imagesDir, "templates"), "*.png").Length;
var outputImageCount = Directory.GetFiles(Path.Combine(imagesDir, "outputs"), "*.png").Length;

Console.WriteLine($"Generated Documents:");
Console.WriteLine($"  Templates: {templateCount} files");
Console.WriteLine($"  Outputs:   {outputCount} files");
Console.WriteLine();

if (!skipImages && (templateImageCount > 0 || outputImageCount > 0))
{
    Console.WriteLine($"Generated Images:");
    Console.WriteLine($"  Templates: {templateImageCount} images");
    Console.WriteLine($"  Outputs:   {outputImageCount} images");
    Console.WriteLine();
    Console.WriteLine("📍 Location:");
    Console.WriteLine($"  Documents: {templatesDir}");
    Console.WriteLine($"  Images:    {imagesDir}");
    Console.WriteLine();
    Console.WriteLine("💡 Usage in documentation (from docs/for-template-authors/*.md):");
    Console.WriteLine("  ![Template](../images/examples/templates/hello-world-template.png)");
    Console.WriteLine("  ![Output](../images/examples/outputs/hello-world-output.png)");
}
else
{
    Console.WriteLine($"📍 Location:");
    Console.WriteLine($"  Documents: {templatesDir}");
}

Console.WriteLine();
if (failures > 0)
{
    Console.WriteLine($"✗ Finished with {failures} failure(s).");
    return 1;
}

Console.WriteLine("✓ All done!");
return 0;

// Helper functions

static string? FindEnvFile(string repositoryRoot)
{
    var dir = Directory.GetCurrentDirectory();
    while (dir != null)
    {
        var envPath = Path.Combine(dir, ".env");
        if (File.Exists(envPath))
        {
            return envPath;
        }

        dir = Directory.GetParent(dir)?.FullName;
    }

    var projectEnvPath = Path.Combine(repositoryRoot, "TriasDev.Templify.DocumentGenerator", ".env");
    return File.Exists(projectEnvPath) ? projectEnvPath : null;
}

static bool GenerateExample(IExampleGenerator generator, string templatesDir, string outputsDir)
{
    Console.WriteLine($"[{generator.Name}]");
    Console.WriteLine($"  Description: {generator.Description}");

    try
    {
        // Generate template
        Console.Write("  Generating template... ");
        var templatePath = generator.GenerateTemplate(templatesDir);
        Console.WriteLine($"✓ {Path.GetFileName(templatePath)}");

        // Process template
        Console.Write("  Processing with Templify... ");
        var outputPath = generator.ProcessTemplate(templatePath, outputsDir);
        Console.WriteLine($"✓ {Path.GetFileName(outputPath)}");

        Console.WriteLine();
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Error: {ex.Message}");
        Console.WriteLine();
        return false;
    }
}

static async Task<int> ConvertDocumentsToImages(StirlingPdfConverter converter, string sourceDir, string targetDir, string label)
{
    Console.WriteLine($"Converting {label}...");

    var docxFiles = Directory.GetFiles(sourceDir, "*.docx");
    var successCount = 0;

    foreach (var docxFile in docxFiles)
    {
        var fileName = Path.GetFileNameWithoutExtension(docxFile);
        var pngPath = Path.Combine(targetDir, $"{fileName}.png");

        try
        {
            Console.Write($"  {Path.GetFileName(docxFile)} → ");
            var pngBytes = await converter.ConvertDocxToPngAsync(docxFile);
            await File.WriteAllBytesAsync(pngPath, pngBytes);
            Console.WriteLine($"✓ {fileName}.png");
            successCount++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
        }
    }

    Console.WriteLine($"  Converted: {successCount}/{docxFiles.Length}");
    Console.WriteLine();
    return docxFiles.Length - successCount;
}
