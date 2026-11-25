// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// Describes one template in code. <see cref="TemplateBuilder.Build"/> runs <see cref="Compose"/> on every build,
/// so one definition yields every language and format.
/// </summary>
/// <remarks>
/// <see cref="Compose"/> must not have side effects: a build is a pure function of the definition and the
/// <see cref="TemplateBuildOptions"/>. Pass product data that the template depends on, such as a customer's theme,
/// through the definition's constructor.
/// </remarks>
public interface ITemplateDefinition
{
    /// <summary>Gets the identity of the template: a stable id and the version of its definition.</summary>
    TemplateIdentity Identity { get; }

    /// <summary>Describes the template's content.</summary>
    /// <param name="template">The composer to describe the template with. It is valid only during this call.</param>
    void Compose(TemplateComposer template);
}
