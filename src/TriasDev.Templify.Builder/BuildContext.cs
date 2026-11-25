// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// State shared by the composers of one build: the diagnostics collected while composing.
/// </summary>
internal sealed class BuildContext
{
    private readonly List<BuildDiagnostic> _errors = new List<BuildDiagnostic>();

    /// <summary>Gets the errors found so far.</summary>
    public IReadOnlyList<BuildDiagnostic> Errors => _errors;

    /// <summary>Records an error; the build fails after composing, with every error found.</summary>
    public void Error(string code, string message) => _errors.Add(new BuildDiagnostic(code, message));

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

    /// <summary>Content uses a style the style sheet does not define.</summary>
    public const string UndefinedStyle = "TB2010";
}
