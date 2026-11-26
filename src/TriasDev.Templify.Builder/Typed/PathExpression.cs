// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TriasDev.Templify.Builder;

/// <summary>Turns a member expression (<c>o =&gt; o.Order.Customer.Name</c>) into a relative data path.</summary>
internal static class PathExpression
{
    /// <summary>The dotted path the expression reads, named as the data is (JSON names); empty for <c>o =&gt; o</c>.</summary>
    /// <exception cref="ArgumentException">The expression is not a chain of properties and constant dictionary keys.</exception>
    public static string Relative(LambdaExpression lambda, JsonNamingPolicy? naming)
    {
        ArgumentNullException.ThrowIfNull(lambda);
        JsonNamingPolicy policy = naming ?? JsonNamingPolicy.CamelCase;
        List<string> segments = new List<string>();
        string index = string.Empty;
        Expression expression = lambda.Body;
        while (true)
        {
            switch (expression)
            {
                case UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert:
                    expression = convert.Operand;
                    continue;

                case MemberExpression { Expression: not null, Member: PropertyInfo { Name: "Value" } } nullable
                    when nullable.Member.DeclaringType is { IsGenericType: true } declaring && declaring.GetGenericTypeDefinition() == typeof(Nullable<>):
                    // o.Discount.Value reads the same data as o.Discount.
                    expression = nullable.Expression;
                    continue;

                case MemberExpression { Expression: not null, Member: PropertyInfo or FieldInfo } member:
                    if (member.Member.DeclaringType?.Namespace is { } ns && (ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal)))
                    {
                        throw Unsupported(lambda, $"'{member.Member.Name}' is computed by {member.Member.DeclaringType.Name}, not a value in the data");
                    }

                    segments.Add((member.Member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? policy.ConvertName(member.Member.Name)) + index);
                    index = string.Empty;
                    expression = member.Expression;
                    continue;

                case MethodCallExpression { Method.Name: "get_Item", Object: not null, Arguments.Count: 1 } indexer:
                    if (indexer.Arguments[0] is ConstantExpression { Value: string key })
                    {
                        if (key.Length == 0 || !key.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
                        {
                            throw Unsupported(lambda, $"the dictionary key '{key}' is not one path segment (letters, digits and '_')");
                        }

                        segments.Add(key + index);
                        index = string.Empty;
                        expression = indexer.Object;
                        continue;
                    }

                    if (indexer.Arguments[0] is ConstantExpression { Value: int position } && index.Length == 0)
                    {
                        // o.Items[1].Name: the index belongs to the segment of the list, which comes next.
                        index = "[" + position.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                        expression = indexer.Object;
                        continue;
                    }

                    throw Unsupported(lambda, indexer.Arguments[0].Type == typeof(string) ? "a dictionary key must be a constant string" : "a list index must be a constant number, and only one per list");

                case BinaryExpression { NodeType: ExpressionType.ArrayIndex } array:
                    if (array.Right is ConstantExpression { Value: int arrayPosition } && index.Length == 0)
                    {
                        index = "[" + arrayPosition.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                        expression = array.Left;
                        continue;
                    }

                    throw Unsupported(lambda, "a list index must be a constant number, and only one per list");

                case ParameterExpression parameter when parameter == lambda.Parameters[0] && index.Length == 0:
                    segments.Reverse();
                    return string.Join(".", segments);

                default:
                    throw Unsupported(lambda, $"'{expression}' is not a property");
            }
        }
    }

    private static ArgumentException Unsupported(LambdaExpression lambda, string reason) =>
        new ArgumentException($"The expression '{lambda}' cannot be turned into a data path: {reason}. Use a chain of properties (o => o.Order.Customer.Name).");
}
