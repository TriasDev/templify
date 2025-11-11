using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Visitors;

/// <summary>
/// Composite visitor that dispatches to multiple child visitors.
/// Enables composition of specialized visitors for different template constructs.
/// </summary>
/// <remarks>
/// The CompositeVisitor implements the Composite pattern for visitors.
/// It allows combining multiple specialized visitors (ConditionalVisitor,
/// LoopVisitor, PlaceholderVisitor) into a single visitor.
///
/// Benefits:
/// - Clean separation of concerns: each visitor handles one construct type
/// - Flexible composition: different combinations for different use cases
/// - Reusable: same visitors can be composed differently
/// - Testable: each visitor can be tested independently
///
/// Usage:
/// ```csharp
/// var composite = new CompositeVisitor(
///     new ConditionalVisitor(),
///     new LoopVisitor(walker, nestedComposite),
///     new PlaceholderVisitor(options, missingVars)
/// );
/// walker.Walk(document, composite, context);
/// ```
/// </remarks>
internal sealed class CompositeVisitor : ITemplateElementVisitor
{
    private readonly IReadOnlyList<ITemplateElementVisitor> _visitors;

    public CompositeVisitor(params ITemplateElementVisitor[] visitors)
    {
        if (visitors == null || visitors.Length == 0)
        {
            throw new ArgumentException("At least one visitor must be provided.", nameof(visitors));
        }

        _visitors = visitors;
    }

    public CompositeVisitor(IEnumerable<ITemplateElementVisitor> visitors)
    {
        if (visitors == null)
        {
            throw new ArgumentNullException(nameof(visitors));
        }

        _visitors = visitors.ToList();

        if (_visitors.Count == 0)
        {
            throw new ArgumentException("At least one visitor must be provided.", nameof(visitors));
        }
    }

    /// <summary>
    /// Dispatches conditional block visit to all child visitors.
    /// </summary>
    public void VisitConditional(ConditionalBlock conditional, IEvaluationContext context)
    {
        foreach (ITemplateElementVisitor visitor in _visitors)
        {
            visitor.VisitConditional(conditional, context);
        }
    }

    /// <summary>
    /// Dispatches loop block visit to all child visitors.
    /// </summary>
    public void VisitLoop(LoopBlock loop, IEvaluationContext context)
    {
        foreach (ITemplateElementVisitor visitor in _visitors)
        {
            visitor.VisitLoop(loop, context);
        }
    }

    /// <summary>
    /// Dispatches placeholder visit to all child visitors.
    /// </summary>
    public void VisitPlaceholder(PlaceholderMatch placeholder, Paragraph paragraph, IEvaluationContext context)
    {
        foreach (ITemplateElementVisitor visitor in _visitors)
        {
            visitor.VisitPlaceholder(placeholder, paragraph, context);
        }
    }

    /// <summary>
    /// Dispatches paragraph visit to all child visitors.
    /// </summary>
    public void VisitParagraph(Paragraph paragraph, IEvaluationContext context)
    {
        foreach (ITemplateElementVisitor visitor in _visitors)
        {
            visitor.VisitParagraph(paragraph, context);
        }
    }
}
