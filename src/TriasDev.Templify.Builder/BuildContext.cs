// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// State shared by the composers of one build: the diagnostics collected while composing.
/// </summary>
internal sealed class BuildContext
{
    private readonly List<BuildDiagnostic> _errors = new List<BuildDiagnostic>();
    private readonly List<BuildDiagnostic> _warnings = new List<BuildDiagnostic>();

    /// <summary>Gets the warnings found so far; they do not fail the build.</summary>
    public IReadOnlyList<BuildDiagnostic> Warnings => _warnings;

    /// <summary>Records a warning: the template builds, but something behaves differently than one might expect.</summary>
    public void Warn(string code, string message) => _warnings.Add(new BuildDiagnostic(code, message));

    /// <summary>Gets the errors found so far.</summary>
    public IReadOnlyList<BuildDiagnostic> Errors => _errors;

    /// <summary>Records an error; the build fails after composing, with every error found.</summary>
    public void Error(string code, string message) => _errors.Add(new BuildDiagnostic(code, message) { Location = CurrentLocation });

    /// <summary>Gets or sets the build's texts (for components).</summary>
    public TemplateTexts? Texts { get; set; }

    /// <summary>Gets the objects a definition shares with its components, by type.</summary>
    public Dictionary<Type, object> Shared { get; } = new Dictionary<Type, object>();

    /// <summary>Where a JSON definition is being composed (a JSON pointer); null for C# definitions.</summary>
    public string? CurrentLocation { get; set; }

    /// <summary>The JSON pointers of model blocks a JSON definition created, for checks that run on the finished model.</summary>
    public System.Runtime.CompilerServices.ConditionalWeakTable<Model.Block, string> Locations { get; } = new System.Runtime.CompilerServices.ConditionalWeakTable<Model.Block, string>();

    /// <summary>Sets <see cref="CurrentLocation"/> to a block's JSON pointer (null when it has none).</summary>
    public void At(Model.Block block) => CurrentLocation = Locations.TryGetValue(block, out string? location) ? location : null;

    /// <summary>Records an error at a location in a JSON definition.</summary>
    public void Error(string code, string message, string? location) => _errors.Add(new BuildDiagnostic(code, message) { Location = location });

    /// <summary>Records problems found elsewhere, for example in a style sheet.</summary>
    public void AddRange(IEnumerable<BuildDiagnostic> problems) => _errors.AddRange(problems);
}

/// <summary>Stable diagnostic codes.</summary>
internal static class DiagnosticCodes
{
    /// <summary>The definition has no identity.</summary>
    public const string MissingIdentity = "TB0001";

    /// <summary>The identity's id is empty.</summary>
    public const string EmptyIdentityId = "TB0002";

    /// <summary>A placeholder was given the empty (root) path.</summary>
    public const string EmptyValuePath = "TB1001";

    /// <summary>Literal text contains <c>{{</c>, which Templify would read as the start of a placeholder.</summary>
    public const string LiteralOpenBraces = "TB1002";

    /// <summary><see cref="TemplateComposer.Styles"/> was called more than once.</summary>
    public const string StylesAlreadySet = "TB2000";

    /// <summary>A style id is defined more than once.</summary>
    public const string DuplicateStyleId = "TB2001";

    /// <summary>A display name is used by several styles.</summary>
    public const string DuplicateStyleName = "TB2002";

    /// <summary>A style is based on a style the sheet does not define.</summary>
    public const string UnknownBasedOn = "TB2003";

    /// <summary>A paragraph style names a next style the sheet does not define.</summary>
    public const string UnknownNextStyle = "TB2004";

    /// <summary>Styles inherit from each other in a cycle.</summary>
    public const string BasedOnCycle = "TB2005";

    /// <summary>A style's display name contains a comma, which Word reads as a list of aliases (schema-invalid DOCX).</summary>
    public const string InvalidStyleName = "TB2006";

    /// <summary>Content uses a style the style sheet does not define.</summary>
    public const string UndefinedStyle = "TB2010";

    /// <summary>One font family is embedded with two different files.</summary>
    public const string ConflictingEmbeddedFont = "TB2011";

    /// <summary>A switch has no <c>Default</c>.</summary>
    public const string SwitchWithoutDefault = "TB3001";

    /// <summary>A switch names the same value in more than one case.</summary>
    public const string DuplicateSwitchCase = "TB3002";

    /// <summary>A path from a loop variable is used outside its loop.</summary>
    public const string LoopVariableOutOfScope = "TB4001";

    /// <summary>Loop metadata of an outer loop is used inside a nested loop.</summary>
    public const string OuterLoopMetadata = "TB4002";

    /// <summary>A path from the data's root starts with the name of an enclosing loop variable.</summary>
    public const string RootPathHidden = "TB4003";

    /// <summary>A text key has no translation for the build's language.</summary>
    public const string MissingText = "TB5001";

    /// <summary>The JSON export cannot write a text: a literal "[[" (read as a text key) or a key outside the key syntax.</summary>
    public const string TextNotExportable = "TB9204";

    /// <summary><see cref="TemplateComposer.Page"/> was called more than once.</summary>
    public const string PageAlreadySet = "TB6001";

    /// <summary>The margins leave no room for text.</summary>
    public const string NoTextFrame = "TB6002";

    /// <summary>A table has no columns.</summary>
    public const string TableWithoutColumns = "TB7001";

    /// <summary>A table's fixed columns do not fit its container.</summary>
    public const string TableTooWide = "TB7002";

    /// <summary>A row's cells do not span the table's columns.</summary>
    public const string RowCellCount = "TB7003";

    /// <summary>A raw block's XML has a root element that cannot stand at block level.</summary>
    public const string InvalidRawBlock = "TB9101";

    /// <summary>A raw block asks for a placeholder of a path it does not declare.</summary>
    public const string UndeclaredRawPath = "TB9102";

    /// <summary>A JSON definition is malformed: its structure, an unknown key or kind, a wrong value, an unknown asset or component.</summary>
    public const string InvalidJson = "TB9200";

    /// <summary>A value in a JSON definition is not accepted by the builder (a composer rejected it).</summary>
    public const string InvalidJsonValue = "TB9201";

    /// <summary>A JSON definition contains raw XML, which the import options do not allow.</summary>
    public const string RawXmlNotAllowed = "TB9202";

    /// <summary>A JSON definition reads a data path the data catalogue does not list.</summary>
    public const string UnknownDataPath = "TB9203";

    /// <summary>A table of contents is built only by an application that updates it (Word on open; not LibreOffice).</summary>
    public const string TableOfContentsNotBuilt = "TB9001";

    /// <summary>A list item uses a level its list style does not define.</summary>
    public const string UndefinedListLevel = "TB8001";
}
