// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Builder.Model;
using TriasDev.Templify.Builder.Writers;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Builds templates from <see cref="ITemplateDefinition"/>s.
/// </summary>
public static class TemplateBuilder
{
    /// <summary>
    /// Builds a template. The same definition and options always give the same bytes on the same runtime and
    /// operating system family, so the result can be cached by <see cref="GeneratedTemplate.ContentHash"/>.
    /// </summary>
    /// <param name="definition">The template definition.</param>
    /// <param name="options">The format and language to build.</param>
    /// <returns>The built template.</returns>
    /// <exception cref="ArgumentException">The options name an unsupported format or the invariant culture.</exception>
    /// <exception cref="TemplateBuildException">The definition is invalid; the exception lists every problem.</exception>
    /// <remarks>Thread-safe: concurrent builds share no state.</remarks>
    public static GeneratedTemplate Build(ITemplateDefinition definition, TemplateBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        BuildContext context = new BuildContext();
        ValidateIdentity(definition.Identity, context);

        TemplateComposer composer = new TemplateComposer(context);
        definition.Compose(composer);
        IReadOnlyList<Block> body = composer.Close();

        if (context.Errors.Count > 0)
        {
            throw new TemplateBuildException(context.Errors);
        }

        DocumentModel model = new DocumentModel(body, options.Language);
        byte[] content = options.Format == TemplateFormat.Docx ? DocxWriter.Write(model) : OdtWriter.Write(model);
        return new GeneratedTemplate(options.Format, content);
    }

    private static void ValidateOptions(TemplateBuildOptions options)
    {
        if (options.Format is not (TemplateFormat.Docx or TemplateFormat.Odt))
        {
            throw new ArgumentException(
                $"Unsupported template format '{options.Format}'. Use TemplateFormat.Docx or TemplateFormat.Odt.",
                nameof(options));
        }

        if (options.Language is null || string.IsNullOrEmpty(options.Language.Name))
        {
            throw new ArgumentException(
                "The document language must be a named culture (for example \"de-DE\" or \"en\"), not the invariant culture.",
                nameof(options));
        }
    }

    private static void ValidateIdentity(TemplateIdentity? identity, BuildContext context)
    {
        if (identity is null)
        {
            context.Error(DiagnosticCodes.MissingIdentity, "The template definition has no identity (ITemplateDefinition.Identity is null).");
        }
        else if (string.IsNullOrWhiteSpace(identity.Id))
        {
            context.Error(DiagnosticCodes.EmptyIdentityId, "The template identity has an empty id.");
        }
    }
}
