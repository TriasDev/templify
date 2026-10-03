using TriasDev.Templify.DocumentGenerator.Generators;

namespace TriasDev.Templify.DocumentGenerator;

/// <summary>
/// Registry of all example generators.
/// </summary>
public static class ExampleGenerators
{
    /// <summary>
    /// The fixed reference date used in sample data so that generated outputs are deterministic.
    /// </summary>
    public static readonly DateTime SampleDate = new(2025, 1, 15, 10, 30, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// All registered example generators, in generation order.
    /// </summary>
    public static IReadOnlyList<IExampleGenerator> All { get; } =
    [
        new HelloWorldGenerator(),
        new InvoiceGenerator(),
        new ConditionalGenerator(),
        new AdvancedConditionalGenerator(),
        new WarningReportTemplateGenerator(),
        new LibreOfficeLetterGenerator(),
    ];
}
