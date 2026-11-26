// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// Adds a data type to the typed path generator. Types used in <see cref="TemplateComposer.DataAs{T}(System.Text.Json.JsonNamingPolicy)"/> and every type
/// reachable from them are generated anyway; this attribute is for types that are not, for example the parameter types of
/// components in a component library: <c>[assembly: TemplateData(typeof(Address))]</c>.
/// </summary>
/// <param name="type">The data type.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class TemplateDataAttribute(Type type) : Attribute
{
    /// <summary>Gets the data type.</summary>
    public Type Type { get; } = type;
}
