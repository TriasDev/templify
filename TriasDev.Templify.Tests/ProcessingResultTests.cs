// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests;

public class ProcessingResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        // Act
        ProcessingResult result = ProcessingResult.Success(5);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.ReplacementCount);
        Assert.Null(result.ErrorMessage);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void Success_WithMissingVariables_IncludesThem()
    {
        // Arrange
        List<string> missingVars = new List<string> { "Var1", "Var2" };

        // Act
        ProcessingResult result = ProcessingResult.Success(3, missingVars);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);
        Assert.Equal(2, result.MissingVariables.Count);
        Assert.Contains("Var1", result.MissingVariables);
        Assert.Contains("Var2", result.MissingVariables);
    }

    [Fact]
    public void Failure_CreatesFailedResult()
    {
        // Act
        ProcessingResult result = ProcessingResult.Failure("Test error");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.ReplacementCount);
        Assert.Equal("Test error", result.ErrorMessage);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void Init_AllowsPropertyInitialization()
    {
        // Act
        ProcessingResult result = new ProcessingResult
        {
            IsSuccess = true,
            ReplacementCount = 10,
            ErrorMessage = "Custom message",
            MissingVariables = new List<string> { "Test" }
        };

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.ReplacementCount);
        Assert.Equal("Custom message", result.ErrorMessage);
        Assert.Single(result.MissingVariables);
    }
}
