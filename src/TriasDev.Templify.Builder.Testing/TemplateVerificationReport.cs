// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Testing;

/// <summary>The check a verification failure belongs to.</summary>
public enum VerificationCheck
{
    /// <summary>The definition did not build.</summary>
    Build,

    /// <summary><c>ValidateTemplate</c> reported errors.</summary>
    Structure,

    /// <summary>The template reads data its contract does not declare, or the witness fill failed.</summary>
    Contract,

    /// <summary>A fill with sample data failed or warned.</summary>
    Fill,

    /// <summary>The DOCX schema validation or the ODT package check failed (template or filled document).</summary>
    Package,

    /// <summary>A second build gave different bytes.</summary>
    Determinism,

    /// <summary>Languages or formats have different data contracts.</summary>
    ContractParity,

    /// <summary>The build reported a warning, and <see cref="TemplateVerificationOptions.FailOnBuildWarnings"/> is set.</summary>
    BuildWarning,
}

/// <summary>One failed check.</summary>
/// <param name="Language">The language of the build, or null for a check across builds.</param>
/// <param name="Format">The format of the build, or null for a check across builds.</param>
/// <param name="Check">The check that failed.</param>
/// <param name="Message">What failed.</param>
public sealed record TemplateVerificationFailure(CultureInfo? Language, TemplateFormat? Format, VerificationCheck Check, string Message)
{
    /// <inheritdoc/>
    public override string ToString()
    {
        string target = Language is null ? "all" : Language.Name + "/" + Format?.ToString().ToLowerInvariant();
        return $"[{target}] {Check}: {Message}";
    }
}

/// <summary>A template the verification built.</summary>
/// <param name="Language">The language it was built in.</param>
/// <param name="Format">Its format.</param>
/// <param name="Template">The built template.</param>
public sealed record VerifiedTemplate(CultureInfo Language, TemplateFormat Format, GeneratedTemplate Template);

/// <summary>The result of <see cref="TemplateVerifier.Verify"/>.</summary>
public sealed class TemplateVerificationReport
{
    internal TemplateVerificationReport(IReadOnlyList<VerifiedTemplate> templates, IReadOnlyList<TemplateVerificationFailure> failures)
    {
        Templates = templates;
        Failures = failures;
    }

    /// <summary>Gets the templates that built, one per language and format.</summary>
    public IReadOnlyList<VerifiedTemplate> Templates { get; }

    /// <summary>Gets the failed checks; empty when everything passed.</summary>
    public IReadOnlyList<TemplateVerificationFailure> Failures { get; }

    /// <summary>Gets a value indicating whether every check passed.</summary>
    public bool IsSuccess => Failures.Count == 0;

    /// <summary>Returns the failures, one per line, or a success line.</summary>
    public override string ToString()
    {
        if (IsSuccess)
        {
            return $"Template verification passed ({Templates.Count} templates).";
        }

        StringBuilder text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"Template verification failed ({Failures.Count} failures):");
        foreach (TemplateVerificationFailure failure in Failures)
        {
            text.AppendLine().Append("  ").Append(failure);
        }

        return text.ToString();
    }
}

/// <summary>Thrown by <see cref="TemplateVerifier.VerifyAll"/> when a check fails; the message lists every failure.</summary>
public sealed class TemplateVerificationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="TemplateVerificationException"/> class.</summary>
    /// <param name="report">The failed report.</param>
    public TemplateVerificationException(TemplateVerificationReport report)
        : base((report ?? throw new ArgumentNullException(nameof(report))).ToString())
    {
        Report = report;
    }

    /// <summary>Gets the report.</summary>
    public TemplateVerificationReport Report { get; }
}
