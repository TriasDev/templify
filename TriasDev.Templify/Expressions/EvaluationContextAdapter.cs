using TriasDev.Templify.Core;

namespace TriasDev.Templify.Expressions;

/// <summary>
/// Adapts IEvaluationContext to IDataContext for expression evaluation.
/// </summary>
internal sealed class EvaluationContextAdapter : IDataContext
{
    private readonly IEvaluationContext _context;

    public EvaluationContextAdapter(IEvaluationContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public object? GetValue(string variableName)
    {
        _context.TryResolveVariable(variableName, out object? value);
        return value;
    }
}
