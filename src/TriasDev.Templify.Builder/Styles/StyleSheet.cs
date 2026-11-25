// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>
/// The named styles of a template: defaults, paragraph styles and character styles. Both writers turn them into
/// real named styles, so a downloaded template stays editable in Word and LibreOffice.
/// </summary>
/// <remarks>
/// A style sheet is immutable and can be shared by many definitions and builds. Build it from a product's theme,
/// for example in a method <c>StyleSheet Create(BrandTheme theme)</c>, so a customer's theme slots flow into the
/// styles. Problems that involve several styles (a duplicate id, an unknown <c>BasedOn</c>, a cycle) are reported by
/// <see cref="TemplateBuilder.Build"/>, together with every other problem of the definition.
/// </remarks>
public sealed class StyleSheet
{
    private StyleSheet(
        TextProperties defaults,
        IReadOnlyList<ParagraphStyleDefinition> paragraphStyles,
        IReadOnlyList<CharacterStyleDefinition> characterStyles,
        IReadOnlyList<TableStyleDefinition> tableStyles,
        IReadOnlyList<BoxStyleDefinition> boxStyles,
        IReadOnlyList<ListStyleDefinition> listStyles)
    {
        BoxStyles = boxStyles;
        ListStyles = listStyles;
        Defaults = defaults;
        ParagraphStyles = paragraphStyles;
        CharacterStyles = characterStyles;
        TableStyles = tableStyles;
        Problems = Validate(paragraphStyles, characterStyles);
        foreach (IGrouping<string, string> duplicate in tableStyles.Select(t => t.Key.Id).Concat(boxStyles.Select(b => b.Key.Id)).Concat(listStyles.Select(l => l.Key.Id)).GroupBy(id => id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            ((List<BuildDiagnostic>)Problems).Add(new BuildDiagnostic(DiagnosticCodes.DuplicateStyleId, $"The table, box or list style id '{duplicate.Key}' is defined more than once."));
        }
    }

    /// <summary>Gets the list styles in definition order.</summary>
    internal IReadOnlyList<ListStyleDefinition> ListStyles { get; }

    /// <summary>Finds the definition of a list style.</summary>
    internal ListStyleDefinition? Find(ListStyleKey key) => ListStyles.FirstOrDefault(s => s.Key == key);

    /// <summary>Gets the box styles in definition order.</summary>
    internal IReadOnlyList<BoxStyleDefinition> BoxStyles { get; }

    /// <summary>Finds the definition of a box style.</summary>
    internal BoxStyleDefinition? Find(BoxStyleKey key) => BoxStyles.FirstOrDefault(s => s.Key == key);

    /// <summary>The effective style of a table: its box style or its table style; null when it has none or it is undefined.</summary>
    internal TableStyleDefinition? Resolve(TableBlock table) =>
        table.BoxStyle is { } box ? Find(box)?.AsTableStyle()
        : table.Style is { } key ? Find(key)
        : null;

    /// <summary>The space after a table: its own, else its box style's.</summary>
    internal Length? SpaceAfterOf(TableBlock table) =>
        table.SpaceAfter ?? (table.BoxStyle is { } box ? Find(box)?.SpaceAfter : null);

    /// <summary>Gets the table styles in definition order.</summary>
    internal IReadOnlyList<TableStyleDefinition> TableStyles { get; }

    /// <summary>Finds the definition of a table style.</summary>
    internal TableStyleDefinition? Find(TableStyleKey key) => TableStyles.FirstOrDefault(s => s.Key == key);

    /// <summary>Gets the default text formatting.</summary>
    internal TextProperties Defaults { get; }

    /// <summary>Gets the paragraph styles in definition order.</summary>
    internal IReadOnlyList<ParagraphStyleDefinition> ParagraphStyles { get; }

    /// <summary>Gets the character styles in definition order.</summary>
    internal IReadOnlyList<CharacterStyleDefinition> CharacterStyles { get; }

    /// <summary>Gets the problems found in the style sheet; reported when a template uses it.</summary>
    internal IReadOnlyList<BuildDiagnostic> Problems { get; }

    /// <summary>Gets an empty style sheet: default formatting of the application that opens the document.</summary>
    internal static StyleSheet Empty { get; } = new StyleSheet(new TextProperties(), [], [], [], [], []);

    /// <summary>Creates a style sheet.</summary>
    /// <param name="styles">Defines the defaults and the styles.</param>
    /// <returns>The style sheet.</returns>
    public static StyleSheet Create(Action<StyleSheetComposer> styles)
    {
        ArgumentNullException.ThrowIfNull(styles);
        StyleSheetComposer composer = new StyleSheetComposer();
        styles(composer);
        composer.Close();
        return new StyleSheet(composer.DefaultText, composer.ParagraphStyles, composer.CharacterStyles, composer.TableStyles, composer.BoxStyles, composer.ListStyles);
    }

    /// <summary>A copy of this style sheet with one more paragraph style (the builder's own default styles).</summary>
    internal StyleSheet WithParagraphStyle(ParagraphStyleDefinition definition) =>
        new StyleSheet(Defaults, ParagraphStyles.Append(definition).ToList(), CharacterStyles, TableStyles, BoxStyles, ListStyles);

    /// <summary>Finds the definition of a paragraph style.</summary>
    /// <summary>Every font face the styles use, defaults first, in definition order (with repeats).</summary>
    internal IEnumerable<FontFace> Fonts => new[] { Defaults.Font }
        .Concat(ParagraphStyles.Select(s => s.Text.Font))
        .Concat(CharacterStyles.Select(s => s.Text.Font))
        .OfType<FontFace>();

    /// <summary>The embedded font of each family that embeds one, in first-use order.</summary>
    internal IReadOnlyList<FontFace> EmbeddedFonts => Fonts
        .Where(f => f.IsEmbedded)
        .DistinctBy(f => f.Family, StringComparer.Ordinal)
        .ToList();

    internal ParagraphStyleDefinition? Find(ParagraphStyleKey key) => ParagraphStyles.FirstOrDefault(s => s.Key == key);

    /// <summary>Finds the definition of a character style.</summary>
    internal CharacterStyleDefinition? Find(CharacterStyleKey key) => CharacterStyles.FirstOrDefault(s => s.Key == key);

    /// <summary>The start indent of a paragraph style, through its <c>BasedOn</c> chain; zero when none is set.</summary>
    internal Length IndentStartOf(ParagraphStyleKey? key)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (ParagraphStyleDefinition? style = key is null ? null : Find(key); style != null && seen.Add(style.Key.Id); style = style.BasedOn is { } b ? Find(b) : null)
        {
            if (style.Paragraph.IndentStart is { } indent)
            {
                return indent;
            }
        }

        return Length.Zero;
    }

    /// <summary>
    /// The outline level of a paragraph style, through its <c>BasedOn</c> chain; null when it is not a heading.
    /// </summary>
    internal int? OutlineLevelOf(ParagraphStyleKey key)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (ParagraphStyleDefinition? style = Find(key); style != null && seen.Add(style.Key.Id); style = style.BasedOn is { } b ? Find(b) : null)
        {
            if (style.Paragraph.OutlineLevel is int level)
            {
                return level;
            }
        }

        return null;
    }

    private static List<BuildDiagnostic> Validate(
        IReadOnlyList<ParagraphStyleDefinition> paragraphStyles,
        IReadOnlyList<CharacterStyleDefinition> characterStyles)
    {
        List<BuildDiagnostic> problems = new List<BuildDiagnostic>();

        // Word keeps style ids and names in one namespace for all style types; ODF per family. The stricter rule holds.
        IEnumerable<(string Id, string Name, string Kind)> all = paragraphStyles.Select(s => (s.Key.Id, s.Key.DisplayName, "paragraph"))
            .Concat(characterStyles.Select(s => (s.Key.Id, s.Key.DisplayName, "character")));
        foreach (IGrouping<string, (string Id, string Name, string Kind)> group in all.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            problems.Add(new BuildDiagnostic(
                DiagnosticCodes.DuplicateStyleId,
                $"The style id '{group.Key}' is defined more than once ({string.Join(", ", group.Select(s => s.Kind + " style '" + s.Name + "'"))}). Style ids are unique across paragraph and character styles."));
        }

        foreach (IGrouping<string, (string Id, string Name, string Kind)> group in all.GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1 && g.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
        {
            problems.Add(new BuildDiagnostic(
                DiagnosticCodes.DuplicateStyleName,
                $"The display name '{group.Key}' is used by several styles ({string.Join(", ", group.Select(s => s.Id))}). Word requires unique style names."));
        }

        foreach (ParagraphStyleDefinition style in paragraphStyles)
        {
            if (style.BasedOn is { } basedOn && !paragraphStyles.Any(s => s.Key == basedOn))
            {
                problems.Add(new BuildDiagnostic(
                    DiagnosticCodes.UnknownBasedOn,
                    $"Paragraph style '{style.Key.Id}' is based on '{basedOn.Id}', which the style sheet does not define."));
            }

            if (style.Next is { } next && !paragraphStyles.Any(s => s.Key == next))
            {
                problems.Add(new BuildDiagnostic(
                    DiagnosticCodes.UnknownNextStyle,
                    $"Paragraph style '{style.Key.Id}' names '{next.Id}' as its next style, which the style sheet does not define."));
            }
        }

        foreach (CharacterStyleDefinition style in characterStyles)
        {
            if (style.BasedOn is { } basedOn && !characterStyles.Any(s => s.Key == basedOn))
            {
                problems.Add(new BuildDiagnostic(
                    DiagnosticCodes.UnknownBasedOn,
                    $"Character style '{style.Key.Id}' is based on '{basedOn.Id}', which the style sheet does not define."));
            }
        }

        AddCycles(paragraphStyles.Select(s => (s.Key.Id, s.BasedOn?.Id)), "Paragraph", problems);
        AddCycles(characterStyles.Select(s => (s.Key.Id, s.BasedOn?.Id)), "Character", problems);
        return problems;
    }

    private static void AddCycles(IEnumerable<(string Id, string? BasedOn)> styles, string kind, List<BuildDiagnostic> problems)
    {
        Dictionary<string, string?> parents = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach ((string id, string? basedOn) in styles)
        {
            parents.TryAdd(id, basedOn);
        }

        HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (string start in parents.Keys)
        {
            List<string> chain = new List<string>();
            string? current = start;
            while (current != null && parents.ContainsKey(current) && !chain.Contains(current))
            {
                chain.Add(current);
                current = parents[current];
            }

            if (current != null && chain.Contains(current) && current == start && reported.Add(string.Join(">", chain.OrderBy(s => s, StringComparer.Ordinal))))
            {
                problems.Add(new BuildDiagnostic(
                    DiagnosticCodes.BasedOnCycle,
                    $"{kind} styles inherit from each other in a cycle: {string.Join(" → ", chain)} → {start}."));
            }
        }
    }
}
