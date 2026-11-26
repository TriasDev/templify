// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder;

/// <summary>Whether a stored template still works with the current data.</summary>
public enum TemplateCheckStatus
{
    /// <summary>No problems.</summary>
    Ok = 0,

    /// <summary>The template works, but is outdated or of unknown origin (only warnings).</summary>
    Warning = 1,

    /// <summary>The template will not fill correctly (at least one error).</summary>
    Broken = 2,
}

/// <summary>How serious a problem of a check is.</summary>
public enum TemplateCheckSeverity
{
    /// <summary>The template works, but should be looked at.</summary>
    Warning = 0,

    /// <summary>The template will not fill correctly.</summary>
    Error = 1,
}

/// <summary>One problem a check found.</summary>
/// <param name="Code">The code: TB93xx for documents (see the developer docs), the build's codes for JSON definitions.</param>
/// <param name="Severity">How serious it is.</param>
/// <param name="Message">What is wrong, in English.</param>
public sealed record TemplateCheckProblem(string Code, TemplateCheckSeverity Severity, string Message)
{
    /// <summary>Gets the data path concerned, as the template writes it (with its loop variable names); null when none.</summary>
    public string? DataPath { get; init; }

    /// <summary>Gets where the problem is: the engine's location in a document, the JSON pointer in a definition; null when unknown.</summary>
    public string? Location { get; init; }

    /// <inheritdoc/>
    public override string ToString() => $"{Code}: {Message}";
}

/// <summary>What to check a stored template against.</summary>
public sealed record TemplateCheckOptions
{
    /// <summary>Gets the current data: every path a template may read.</summary>
    public required DataCatalog Data { get; init; }

    /// <summary>Gets the identity the template should have (the current definition); null to leave the stamp's identity unjudged.</summary>
    public TemplateIdentity? Expected { get; init; }

    /// <summary>
    /// Gets the current components, to compare a copy's component versions with; null to leave them unjudged. Every
    /// versioned component the host uses should be registered.
    /// </summary>
    public ComponentCatalog? Components { get; init; }
}

/// <summary>The result of <see cref="TemplateInspector.Check"/>.</summary>
public sealed class TemplateCheckResult
{
    internal TemplateCheckResult(TemplateFormat format, TemplateStamp? stamp, IReadOnlyList<TemplateCheckProblem> problems)
    {
        Format = format;
        Stamp = stamp;
        Problems = problems;
        Status = problems.Any(p => p.Severity == TemplateCheckSeverity.Error) ? TemplateCheckStatus.Broken
            : problems.Count > 0 ? TemplateCheckStatus.Warning
            : TemplateCheckStatus.Ok;
    }

    /// <summary>Gets the status: <see cref="TemplateCheckStatus.Broken"/> with any error, <see cref="TemplateCheckStatus.Warning"/> with only warnings.</summary>
    public TemplateCheckStatus Status { get; }

    /// <summary>Gets the problems, errors first.</summary>
    public IReadOnlyList<TemplateCheckProblem> Problems { get; }

    /// <summary>Gets the file format; <see cref="TemplateFormat.Unknown"/> for a JSON definition or an unreadable file.</summary>
    public TemplateFormat Format { get; }

    /// <summary>Gets the builder's stamp of a document; null when it has none, and for a JSON definition.</summary>
    public TemplateStamp? Stamp { get; }
}

/// <summary>The codes of <see cref="TemplateInspector.Check"/>.</summary>
internal static class CheckCodes
{
    /// <summary>The template's syntax is broken, or the file is not a template.</summary>
    public const string Syntax = "TB9300";

    /// <summary>The template reads a path the data does not have.</summary>
    public const string UnknownPath = "TB9301";

    /// <summary>Filling with the data's shape fails, for example a loop over a value.</summary>
    public const string WrongKind = "TB9302";

    /// <summary>The template has no builder stamp.</summary>
    public const string NoStamp = "TB9303";

    /// <summary>The stamp names another template.</summary>
    public const string OtherTemplate = "TB9304";

    /// <summary>The stamp names an older version of the template.</summary>
    public const string OlderVersion = "TB9305";

    /// <summary>A component the template was built from is at an older version, or no longer registered.</summary>
    public const string OlderComponent = "TB9306";
}

