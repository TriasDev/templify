// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Written by the typed path generator: this assembly has generated typed paths for <see cref="Type"/>. A project this
/// assembly gives access to its internals (for example its test project) uses them instead of generating them again.
/// </summary>
/// <param name="type">The data type.</param>
/// <param name="loopItem">True for the properties on <c>LoopItem&lt;T&gt;</c>, false for those on <c>TemplatePath&lt;T&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GeneratedDataPathsAttribute(Type type, bool loopItem) : Attribute
{
    /// <summary>Gets the data type.</summary>
    public Type Type { get; } = type;

    /// <summary>Gets a value indicating whether the generated properties are on <c>LoopItem&lt;T&gt;</c>.</summary>
    public bool LoopItem { get; } = loopItem;
}
