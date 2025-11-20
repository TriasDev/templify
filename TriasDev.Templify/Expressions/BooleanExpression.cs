// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Expressions;

/// <summary>
/// Represents a boolean expression that can be evaluated against a data context.
/// </summary>
internal abstract class BooleanExpression
{
    /// <summary>
    /// Evaluates the expression against the provided data context.
    /// </summary>
    /// <param name="context">The data context for variable resolution.</param>
    /// <returns>The boolean result of the expression.</returns>
    public abstract bool Evaluate(IDataContext context);
}

/// <summary>
/// Represents a simple variable reference expression.
/// </summary>
internal sealed class VariableExpression : BooleanExpression
{
    public string VariableName { get; }

    public VariableExpression(string variableName)
    {
        VariableName = variableName ?? throw new ArgumentNullException(nameof(variableName));
    }

    public override bool Evaluate(IDataContext context)
    {
        object? value = context.GetValue(VariableName);
        return value is bool boolValue && boolValue;
    }
}

/// <summary>
/// Represents a logical AND expression.
/// </summary>
internal sealed class AndExpression : BooleanExpression
{
    public BooleanExpression Left { get; }
    public BooleanExpression Right { get; }

    public AndExpression(BooleanExpression left, BooleanExpression right)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public override bool Evaluate(IDataContext context)
    {
        return Left.Evaluate(context) && Right.Evaluate(context);
    }
}

/// <summary>
/// Represents a logical OR expression.
/// </summary>
internal sealed class OrExpression : BooleanExpression
{
    public BooleanExpression Left { get; }
    public BooleanExpression Right { get; }

    public OrExpression(BooleanExpression left, BooleanExpression right)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public override bool Evaluate(IDataContext context)
    {
        return Left.Evaluate(context) || Right.Evaluate(context);
    }
}

/// <summary>
/// Represents a logical NOT expression.
/// </summary>
internal sealed class NotExpression : BooleanExpression
{
    public BooleanExpression Operand { get; }

    public NotExpression(BooleanExpression operand)
    {
        Operand = operand ?? throw new ArgumentNullException(nameof(operand));
    }

    public override bool Evaluate(IDataContext context)
    {
        return !Operand.Evaluate(context);
    }
}

/// <summary>
/// Represents a comparison expression.
/// </summary>
internal sealed class ComparisonExpression : BooleanExpression
{
    public string VariableName { get; }
    public ComparisonOperator Operator { get; }
    public object? Value { get; }

    public ComparisonExpression(string variableName, ComparisonOperator op, object? value)
    {
        VariableName = variableName ?? throw new ArgumentNullException(nameof(variableName));
        Operator = op;
        Value = value;
    }

    public override bool Evaluate(IDataContext context)
    {
        object? leftValue = context.GetValue(VariableName);

        return Operator switch
        {
            ComparisonOperator.Equal => Equals(leftValue, Value),
            ComparisonOperator.NotEqual => !Equals(leftValue, Value),
            ComparisonOperator.GreaterThan => Compare(leftValue, Value) > 0,
            ComparisonOperator.GreaterThanOrEqual => Compare(leftValue, Value) >= 0,
            ComparisonOperator.LessThan => Compare(leftValue, Value) < 0,
            ComparisonOperator.LessThanOrEqual => Compare(leftValue, Value) <= 0,
            _ => false
        };
    }

    private static int Compare(object? left, object? right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        if (left is IComparable leftComparable && right is IComparable)
        {
            try
            {
                return leftComparable.CompareTo(right);
            }
            catch (ArgumentException)
            {
                return 0;
            }
            catch (InvalidCastException)
            {
                return 0;
            }
        }

        return 0;
    }
}

/// <summary>
/// Comparison operators for expressions.
/// </summary>
internal enum ComparisonOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

/// <summary>
/// Interface for data context used in expression evaluation.
/// </summary>
internal interface IDataContext
{
    object? GetValue(string variableName);
}
