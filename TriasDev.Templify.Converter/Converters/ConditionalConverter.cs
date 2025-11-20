using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using HighlightColorValues = DocumentFormat.OpenXml.Wordprocessing.HighlightColorValues;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Converts conditional content controls to Templify if/else blocks.
/// </summary>
public class ConditionalConverter
{
    /// <summary>
    /// Convert a conditional content control to Templify syntax.
    /// </summary>
    /// <param name="sdt">The content control element.</param>
    /// <param name="tag">The tag value (e.g., "conditionalRemove_process.division").</param>
    /// <returns>True if conversion was successful.</returns>
    public bool Convert(SdtElement sdt, string tag)
    {
        if (!tag.StartsWith("conditionalRemove_"))
        {
            return false;
        }

        // Parse the conditional tag
        string condition = ParseConditionalTag(tag);

        // Insert {{#if condition}} before the control with cyan highlighting
        OpenXmlHelpers.InsertTextBefore(sdt, $"{{{{#if {condition}}}}}", HighlightColorValues.Cyan);

        // Unwrap the content control and get the last moved element
        OpenXmlElement? lastMovedElement = OpenXmlHelpers.UnwrapContentControl(sdt);

        // Insert {{/if}} after the last moved element
        if (lastMovedElement != null)
        {
            OpenXmlHelpers.InsertTextAfter(lastMovedElement, "{{/if}}", HighlightColorValues.Cyan);
        }

        return true;
    }

    /// <summary>
    /// Parse a conditional tag to extract the condition expression.
    /// </summary>
    private string ParseConditionalTag(string tag)
    {
        // Remove the "conditionalRemove_" prefix
        string remainder = tag.Substring("conditionalRemove_".Length);

        // Split by underscore
        string[] parts = remainder.Split('_');

        // First part is always the variable path
        string variablePath = parts[0];

        if (parts.Length == 1)
        {
            // Simple existence check: conditionalRemove_field -> {{#if field}}
            return variablePath;
        }

        // Parse operators and values
        List<string> operators = new();
        List<string> values = new();

        for (int i = 1; i < parts.Length; i++)
        {
            string part = parts[i];

            if (IsOperator(part))
            {
                operators.Add(part);

                // If it's a comparison operator, the next part is the value
                if (IsComparisonOperator(part) && i + 1 < parts.Length && !IsOperator(parts[i + 1]))
                {
                    i++;
                    values.Add(parts[i]);
                }
            }
        }

        // Build the condition
        return BuildCondition(variablePath, operators, values);
    }

    /// <summary>
    /// Build a Templify condition string from parsed components.
    /// </summary>
    private string BuildCondition(string variablePath, List<string> operators, List<string> values)
    {
        if (operators.Count == 0)
        {
            return variablePath;
        }

        string condition = variablePath;
        int valueIndex = 0;

        for (int i = 0; i < operators.Count; i++)
        {
            string op = operators[i];

            switch (op)
            {
                case "eq":
                    if (valueIndex < values.Count)
                    {
                        string value = values[valueIndex];
                        string quotedValue = IsNumeric(value) ? value : $"\"{value}\"";
                        condition += $" = {quotedValue}";
                        valueIndex++;
                    }
                    break;

                case "ne":
                    if (valueIndex < values.Count)
                    {
                        string value = values[valueIndex];
                        string quotedValue = IsNumeric(value) ? value : $"\"{value}\"";
                        condition += $" != {quotedValue}";
                        valueIndex++;
                    }
                    break;

                case "gt":
                    if (valueIndex < values.Count)
                    {
                        string value = values[valueIndex];
                        string quotedValue = IsNumeric(value) ? value : $"\"{value}\"";
                        condition += $" > {quotedValue}";
                        valueIndex++;
                    }
                    break;

                case "lt":
                    if (valueIndex < values.Count)
                    {
                        string value = values[valueIndex];
                        string quotedValue = IsNumeric(value) ? value : $"\"{value}\"";
                        condition += $" < {quotedValue}";
                        valueIndex++;
                    }
                    break;

                case "gte":
                    if (valueIndex < values.Count)
                    {
                        string value = values[valueIndex];
                        string quotedValue = IsNumeric(value) ? value : $"\"{value}\"";
                        condition += $" >= {quotedValue}";
                        valueIndex++;
                    }
                    break;

                case "lte":
                    if (valueIndex < values.Count)
                    {
                        string value = values[valueIndex];
                        string quotedValue = IsNumeric(value) ? value : $"\"{value}\"";
                        condition += $" <= {quotedValue}";
                        valueIndex++;
                    }
                    break;

                case "not":
                    // Apply 'not' operator to the condition
                    condition = $"not {condition}";
                    break;

                case "and":
                    condition += " and ";
                    // Next part after 'and' will be added in next iteration
                    if (i + 1 < operators.Count)
                    {
                        // Continue building
                    }
                    break;

                case "or":
                    condition += " or ";
                    // Next part after 'or' will be added in next iteration
                    if (i + 1 < operators.Count)
                    {
                        // Continue building
                    }
                    break;
            }
        }

        return condition;
    }

    /// <summary>
    /// Check if a string is an operator.
    /// </summary>
    private bool IsOperator(string value)
    {
        return value is "eq" or "ne" or "gt" or "lt" or "gte" or "lte" or "and" or "or" or "not";
    }

    /// <summary>
    /// Check if an operator is a comparison operator (requires a value).
    /// </summary>
    private bool IsComparisonOperator(string value)
    {
        return value is "eq" or "ne" or "gt" or "lt" or "gte" or "lte";
    }

    /// <summary>
    /// Check if a string represents a numeric value.
    /// </summary>
    private bool IsNumeric(string value)
    {
        return int.TryParse(value, out _) || double.TryParse(value, out _);
    }
}
