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
        ComposedTemplate composed = Compose(definition, options);
        DocumentModel model = composed.Model;
        byte[] content = options.Format == TemplateFormat.Docx ? DocxWriter.Write(model) : OdtWriter.Write(model);
        return new GeneratedTemplate(options.Format, content, composed.Contract, model.Stamp, composed.Context.Warnings);
    }

    /// <summary>
    /// Runs the definition and every check, and returns the model the writers and the JSON export start from; the
    /// definition's own style sheet is kept apart (without the injected marker style).
    /// </summary>
    internal static ComposedTemplate Compose(ITemplateDefinition definition, TemplateBuildOptions options) => Compose(definition, options, keyMarkers: false);

    /// <summary>Composes a definition; with <paramref name="keyMarkers"/> its texts are key markers (JSON export).</summary>
    internal static ComposedTemplate Compose(ITemplateDefinition definition, TemplateBuildOptions options, bool keyMarkers)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        BuildContext context = new BuildContext();
        ValidateIdentity(definition.Identity, context);

        TemplateComposer composer = new TemplateComposer(context, new TemplateTexts(context, options.Texts ?? TextCatalog.Empty, options.Language, keyMarkers));
        definition.Compose(composer);
        (List<Block> body, StyleSheet styles, DocumentInfo info, PageSetup page) = composer.Close();
        StyleInheritance.Apply(body, page, styles, context);
        StyleSheet userStyles = styles;
        List<Block> allBlocks = body.Concat(page.AllBlocks).ToList();

        if (page.FrameWidth <= Length.Zero)
        {
            context.Error(
                DiagnosticCodes.NoTextFrame,
                $"The margins leave no room for text: the page is {page.Size.Width} wide, the side margins take {page.Left + page.Right}.");
        }

        if (Writers.Spacers.Used(new IReadOnlyList<Block>?[] { body, page.Header, page.Footer, page.FirstHeader, page.FirstFooter }.OfType<IReadOnlyList<Block>>()))
        {
            styles = styles.WithParagraphStyle(BuilderStyles.SpacerDefinition());
        }

        if (BlockTree.HasMarkers(allBlocks) && styles.Find(BuilderStyles.TemplateMarker) == null)
        {
            styles = styles.WithParagraphStyle(BuilderStyles.DefaultMarkerDefinition());
        }

        context.AddRange(styles.Problems);
        foreach (IGrouping<string, FontFace> family in styles.Fonts.Where(f => f.IsEmbedded).Distinct().GroupBy(f => f.Family, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            context.Error(
                DiagnosticCodes.ConflictingEmbeddedFont,
                $"The font family '{family.Key}' is embedded with {family.Count()} different files; a document can embed one file per family.");
        }

        CheckStylesAreDefined(allBlocks, styles, context);
        TableLayout.Check(allBlocks, page.FrameWidth, styles, context);
        context.CurrentLocation = null;

        if (context.Errors.Count > 0)
        {
            throw new TemplateBuildException(context.Errors);
        }

        if (BlockTree.All(allBlocks).OfType<TocBlock>().Any())
        {
            context.Warn(
                DiagnosticCodes.TableOfContentsNotBuilt,
                options.Format == TemplateFormat.Docx
                    ? "The table of contents is a field without a result: Word builds it when it opens the document (fill with UpdateFieldsOnOpen = Auto), and so do converters that refresh indexes on load (for example Stirling-PDF); a plain LibreOffice conversion (soffice --convert-to pdf) does not."
                    : "The table of contents is an index without entries: converters that refresh indexes on load (for example Stirling-PDF) build it; LibreOffice opening the document and a plain conversion (soffice --convert-to pdf) do not, until a user updates it (Tools > Update > Update All).");
        }

        DataContract contract = DataContract.Collect(allBlocks);
        TemplateStamp stamp = TemplateStamp.For(definition.Identity!, contract, options.Language);
        return new ComposedTemplate(new DocumentModel(body, options.Language, styles, info, stamp, page), userStyles, contract, context);
    }

    private static void CheckStylesAreDefined(IReadOnlyList<Block> body, StyleSheet styles, BuildContext context)
    {
        // An undefined inherited style is reported once by the style sheet, not again for every paragraph using it.
        HashSet<string> reported = styles.InheritedStyleUses().Where(u => styles.Find(u.Key) == null).Select(u => "p:" + u.Key.Id).ToHashSet(StringComparer.Ordinal);
        foreach (ParagraphBlock paragraph in BlockTree.Paragraphs(body))
        {
            context.At(paragraph);
            if (paragraph.List is { } item)
            {
                ListStyleDefinition? list = styles.Find(item.Style);
                if (list == null && reported.Add("l:" + item.Style.Id))
                {
                    context.Error(DiagnosticCodes.UndefinedStyle, $"A list uses the list style '{item.Style.Id}', which the template's style sheet does not define.");
                }
                else if (list != null && !list.Levels.ContainsKey(item.Level) && reported.Add($"lv:{item.Style.Id}:{item.Level}"))
                {
                    context.Error(DiagnosticCodes.UndefinedListLevel, $"A list item uses level {item.Level}, which the list style '{item.Style.Id}' does not define.");
                }
                else if (list?.ItemStyle is { } itemStyle && styles.Find(itemStyle) == null && reported.Add("p:" + itemStyle.Id))
                {
                    context.Error(DiagnosticCodes.UndefinedStyle, $"The list style '{item.Style.Id}' uses the item style '{itemStyle.Id}', which the template's style sheet does not define.");
                }
            }

            if (paragraph.Style is { } style && styles.Find(style) == null && reported.Add("p:" + style.Id))
            {
                context.Error(
                    DiagnosticCodes.UndefinedStyle,
                    $"A paragraph uses the style '{style.Id}' (\"{style.DisplayName}\"), which the template's style sheet does not define.");
            }

            foreach (Inline inline in InlineConditional.Descendants(paragraph.Inlines))
            {
                if (inline.Style is { } characterStyle && styles.Find(characterStyle) == null && reported.Add("c:" + characterStyle.Id))
                {
                    context.Error(
                        DiagnosticCodes.UndefinedStyle,
                        $"Text uses the character style '{characterStyle.Id}' (\"{characterStyle.DisplayName}\"), which the template's style sheet does not define.");
                }
            }
        }
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

/// <summary>A composed and checked definition: the model, the definition's own style sheet, the contract and the diagnostics.</summary>
internal sealed record ComposedTemplate(DocumentModel Model, StyleSheet UserStyles, DataContract Contract, BuildContext Context);
