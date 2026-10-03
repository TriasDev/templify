// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Core;

/// <summary>
/// Reports the variables that a condition evaluates for truthiness on their own (bare operands, e.g.
/// <c>{{#if Missing}}</c> or <c>{{#if A and not Missing}}</c>) and that cannot be resolved in the data.
/// Shared by the Word and the OpenDocument validators, which supply the loop-scoped resolution.
/// </summary>
/// <remarks>
/// A missing bare operand silently evaluates to <see langword="false"/>, which usually hides a typo or a data
/// problem, so validation with data reports a <see cref="ValidationWarningType.MissingConditionVariable"/>
/// warning. The warning is not a <see cref="ValidationErrorType.MissingVariable"/> error and does not affect
/// <see cref="ValidationResult.IsValid"/> or <see cref="ValidationResult.MissingVariables"/>.
/// </remarks>
internal static class MissingConditionVariableCheck
{
    /// <summary>
    /// Checks the <c>{{#if}}</c>/<c>{{#elseif}}</c> conditions in <paramref name="text"/> (block, inline and
    /// table-row markers) and adds one warning per condition and missing variable.
    /// </summary>
    /// <param name="text">The text of a paragraph.</param>
    /// <param name="canResolve">Resolves a variable path in the current (loop) scope.</param>
    /// <param name="warnings">The warnings; a warning that is already present is not added again.</param>
    public static void Check(string text, Func<string, bool> canResolve, List<ValidationWarning> warnings)
    {
        if (text.IndexOf("{{#", StringComparison.Ordinal) < 0)
        {
            return;
        }

        ConditionalEvaluator evaluator = new ConditionalEvaluator();
        foreach (Match match in ConditionalPatterns.IfStart.Matches(text).Concat(ConditionalPatterns.ElseIf.Matches(text)))
        {
            string expression = match.Groups[1].Value.Trim();

            // Invalid conditions are reported as errors by the syntax validation.
            if (!evaluator.Validate(expression).IsValid)
            {
                continue;
            }

            foreach (Conditionals.Engine.VariableNode variable in ConditionalEvaluator.CollectBareOperandVariables(ConditionalEvaluator.Parse(expression)))
            {
                string path = variable.Path;

                // Loop metadata and the current item are resolved by the loop, not by the data.
                if (path.StartsWith('@') || path.StartsWith('.') || path == "this" || canResolve(path))
                {
                    continue;
                }

                string message = $"Condition '{expression}' uses variable '{path}', which is not provided in the data. " +
                    "It is treated as false.";
                if (!warnings.Any(w => w.Type == ValidationWarningType.MissingConditionVariable && w.Message == message))
                {
                    warnings.Add(ValidationWarning.Create(ValidationWarningType.MissingConditionVariable, message, match.Value));
                }
            }
        }
    }
}
