// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Benchmarks;

/// <summary>
/// Guards benchmark results so that a failing operation cannot masquerade as a fast one.
/// </summary>
internal static class BenchmarkGuard
{
    /// <summary>
    /// Throws if template processing did not succeed.
    /// </summary>
    public static ProcessingResult EnsureSuccess(ProcessingResult result)
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Template processing failed during benchmark: {result.ErrorMessage}");
        }

        return result;
    }

    /// <summary>
    /// Throws if a condition did not evaluate to the expected value.
    /// </summary>
    public static void EnsureEquals(bool expected, bool actual, string expression)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(
                $"Condition '{expression}' evaluated to {actual}, expected {expected}. The benchmark would measure the wrong code path.");
        }
    }
}
