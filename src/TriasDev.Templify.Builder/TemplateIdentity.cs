// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// The identity of a template definition: a stable id chosen by the product and the version of the definition.
/// </summary>
/// <param name="Id">A stable, non-empty id, for example <c>invoice</c>.</param>
/// <param name="Version">The version of the definition. Increase it when the template changes.</param>
public sealed record TemplateIdentity(string Id, int Version);
