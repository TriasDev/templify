using DotNetEnv;
using TriasDev.Templify.DocumentGenerator;
using TriasDev.Templify.DocumentGenerator.Generators;

// Load environment variables
var envPath = FindEnvFile();
if (envPath != null)
{
    Env.Load(envPath);
}

Console.WriteLine("Templify Documentation Example Generator");
Console.WriteLine("=========================================");
Console.WriteLine();

// Get base directories
var baseDir = Directory.GetCurrentDirectory();
while (!Directory.Exists(Path.Combine(baseDir, "examples")))
{
    var parent = Directory.GetParent(baseDir);
    if (parent == null)
    {
        Console.WriteLine("Error: Could not find 'examples' directory. Run from repository root.");
        return 1;
    }
    baseDir = parent.FullName;
}

var templatesDir = Path.Combine(baseDir, "examples", "templates");
var outputsDir = Path.Combine(baseDir, "examples", "outputs");
var imagesDir = Path.Combine(baseDir, "docfx_project", "images", "examples");

// Ensure directories exist
Directory.CreateDirectory(templatesDir);
Directory.CreateDirectory(outputsDir);
Directory.CreateDirectory(Path.Combine(imagesDir, "templates"));
Directory.CreateDirectory(Path.Combine(imagesDir, "outputs"));

// Register all generators
var generators = new List<IExampleGenerator>
{
    new HelloWorldGenerator(),
    new InvoiceGenerator(),
    new ConditionalGenerator(),
};

// Parse command line arguments
var skipImages = args.Contains("--skip-images");
var requestedExample = args.FirstOrDefault(a => !a.StartsWith("--"));

// Generate documents
if (string.IsNullOrEmpty(requestedExample) || requestedExample == "all")
{
    Console.WriteLine("Generating all examples...");
    Console.WriteLine();

    foreach (var generator in generators)
    {
        GenerateExample(generator, templatesDir, outputsDir);
    }

    Console.WriteLine();
    Console.WriteLine($"✓ Generated {generators.Count} examples successfully!");
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

    GenerateExample(generator, templatesDir, outputsDir);

    Console.WriteLine();
    Console.WriteLine($"✓ Generated '{generator.Name}' successfully!");
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
        Console.WriteLine("  1. Copy .env.example to .env");
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

            await ConvertDocumentsToImages(converter, templatesDir, Path.Combine(imagesDir, "templates"), "templates");
            await ConvertDocumentsToImages(converter, outputsDir, Path.Combine(imagesDir, "outputs"), "outputs");
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
    Console.WriteLine("💡 Usage in documentation:");
    Console.WriteLine("  ![Template](../../images/examples/templates/hello-world-template.png)");
    Console.WriteLine("  ![Output](../../images/examples/outputs/hello-world-output.png)");
}
else
{
    Console.WriteLine($"📍 Location:");
    Console.WriteLine($"  Documents: {templatesDir}");
}

Console.WriteLine();
Console.WriteLine("✓ All done!");

return 0;

// Helper functions

static string? FindEnvFile()
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
    return null;
}

static void GenerateExample(IExampleGenerator generator, string templatesDir, string outputsDir)
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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Error: {ex.Message}");
        Console.WriteLine();
    }
}

static async Task ConvertDocumentsToImages(StirlingPdfConverter converter, string sourceDir, string targetDir, string label)
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
}
