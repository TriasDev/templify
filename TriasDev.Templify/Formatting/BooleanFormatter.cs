// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Formatting;

/// <summary>
/// Formats boolean values as custom string representations.
/// </summary>
public sealed class BooleanFormatter
{
    /// <summary>
    /// Gets the string to display when the boolean value is true.
    /// </summary>
    public string TrueValue { get; }

    /// <summary>
    /// Gets the string to display when the boolean value is false.
    /// </summary>
    public string FalseValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BooleanFormatter"/> class.
    /// </summary>
    /// <param name="trueValue">The string to display for true values.</param>
    /// <param name="falseValue">The string to display for false values.</param>
    public BooleanFormatter(string trueValue, string falseValue)
    {
        TrueValue = trueValue ?? throw new ArgumentNullException(nameof(trueValue));
        FalseValue = falseValue ?? throw new ArgumentNullException(nameof(falseValue));
    }

    /// <summary>
    /// Formats a boolean value using this formatter.
    /// </summary>
    /// <param name="value">The boolean value to format.</param>
    /// <returns>The formatted string representation.</returns>
    public string Format(bool value)
    {
        return value ? TrueValue : FalseValue;
    }
}
