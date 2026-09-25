// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Thrown when a content control cannot be converted automatically. The control is left untouched
/// in the document and reported as needing manual conversion.
/// </summary>
public sealed class ControlConversionException : Exception
{
    /// <summary>
    /// Create a new exception with the reason the control could not be converted.
    /// </summary>
    public ControlConversionException(string message) : base(message)
    {
    }
}
