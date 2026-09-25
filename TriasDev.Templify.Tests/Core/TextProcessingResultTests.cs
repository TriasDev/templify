// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Core;

/// <summary>
/// Unit tests for the <see cref="TextProcessingResult"/> factory methods.
/// </summary>
public sealed class TextProcessingResultTests
{
    [Fact]
    public void Success_PublicOverload_SetsValuesAndHasNoWarnings()
    {
        TextProcessingResult result = TextProcessingResult.Success("Hello", 2, new[] { "Missing" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello", result.ProcessedText);
        Assert.Equal(2, result.ReplacementCount);
        Assert.Equal(new[] { "Missing" }, result.MissingVariables);
        Assert.Null(result.ErrorMessage);
        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void Success_NullTextAndMissingVariables_DefaultToEmpty()
    {
        TextProcessingResult result = TextProcessingResult.Success(null!, 0);

        Assert.Equal(string.Empty, result.ProcessedText);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void Success_WithWarnings_ExposesWarnings()
    {
        ProcessingWarning warning = ProcessingWarning.MissingVariable("Name");

        TextProcessingResult result = TextProcessingResult.Success(null!, 1, null, new[] { warning });

        Assert.Equal(string.Empty, result.ProcessedText);
        Assert.Empty(result.MissingVariables);
        Assert.True(result.HasWarnings);
        Assert.Same(warning, Assert.Single(result.Warnings));
    }

    [Fact]
    public void Failure_SetsErrorAndEmptyOutput()
    {
        TextProcessingResult result = TextProcessingResult.Failure("boom");

        Assert.False(result.IsSuccess);
        Assert.Equal("boom", result.ErrorMessage);
        Assert.Equal(string.Empty, result.ProcessedText);
        Assert.Equal(0, result.ReplacementCount);
    }
}
