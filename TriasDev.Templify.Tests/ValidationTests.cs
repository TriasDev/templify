using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests;

/// <summary>
/// Unit tests for ValidationResult, ValidationError, and related validation types.
/// </summary>
public sealed class ValidationTests
{
    #region ValidationResult Tests

    [Fact]
    public void ValidationResult_Success_CreatesValidResult()
    {
        // Arrange
        List<string> placeholders = new List<string> { "Name", "Email" };

        // Act
        ValidationResult result = ValidationResult.Success(placeholders);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.AllPlaceholders.Count);
        Assert.Contains("Name", result.AllPlaceholders);
        Assert.Contains("Email", result.AllPlaceholders);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void ValidationResult_Success_WithMissingVariables_IncludesThem()
    {
        // Arrange
        List<string> placeholders = new List<string> { "Name", "Email", "Phone" };
        List<string> missingVars = new List<string> { "Phone" };

        // Act
        ValidationResult result = ValidationResult.Success(placeholders, missingVars);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(3, result.AllPlaceholders.Count);
        Assert.Single(result.MissingVariables);
        Assert.Contains("Phone", result.MissingVariables);
    }

    [Fact]
    public void ValidationResult_Failure_CreatesInvalidResult()
    {
        // Arrange
        List<ValidationError> errors = new List<ValidationError>
        {
            ValidationError.Create(ValidationErrorType.UnmatchedConditionalStart, "Test error")
        };
        List<string> placeholders = new List<string> { "Name" };

        // Act
        ValidationResult result = ValidationResult.Failure(errors, placeholders);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("Test error", result.Errors[0].Message);
        Assert.Equal(1, result.AllPlaceholders.Count);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void ValidationResult_Failure_WithMissingVariables_IncludesThem()
    {
        // Arrange
        List<ValidationError> errors = new List<ValidationError>
        {
            ValidationError.Create(ValidationErrorType.MissingVariable, "Variable 'Age' is missing")
        };
        List<string> placeholders = new List<string> { "Name", "Age" };
        List<string> missingVars = new List<string> { "Age" };

        // Act
        ValidationResult result = ValidationResult.Failure(errors, placeholders, missingVars);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(2, result.AllPlaceholders.Count);
        Assert.Single(result.MissingVariables);
        Assert.Contains("Age", result.MissingVariables);
    }

    [Fact]
    public void ValidationResult_IsValid_ReturnsTrueWhenNoErrors()
    {
        // Arrange
        ValidationResult result = new ValidationResult
        {
            Errors = Array.Empty<ValidationError>(),
            AllPlaceholders = new List<string> { "Test" },
            MissingVariables = Array.Empty<string>()
        };

        // Act & Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidationResult_IsValid_ReturnsFalseWhenErrorsExist()
    {
        // Arrange
        ValidationResult result = new ValidationResult
        {
            Errors = new List<ValidationError>
            {
                ValidationError.Create(ValidationErrorType.InvalidPlaceholderSyntax, "Error")
            },
            AllPlaceholders = new List<string> { "Test" },
            MissingVariables = Array.Empty<string>()
        };

        // Act & Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidationResult_WithMultipleErrors_AllErrorsPresent()
    {
        // Arrange
        List<ValidationError> errors = new List<ValidationError>
        {
            ValidationError.Create(ValidationErrorType.UnmatchedConditionalStart, "Error 1"),
            ValidationError.Create(ValidationErrorType.UnmatchedLoopStart, "Error 2"),
            ValidationError.Create(ValidationErrorType.MissingVariable, "Error 3")
        };
        List<string> placeholders = new List<string> { "Var1", "Var2" };

        // Act
        ValidationResult result = ValidationResult.Failure(errors, placeholders);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.UnmatchedConditionalStart);
        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.UnmatchedLoopStart);
        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.MissingVariable);
    }

    #endregion

    #region ValidationError Tests

    [Fact]
    public void ValidationError_Create_SetsPropertiesCorrectly()
    {
        // Act
        ValidationError error = ValidationError.Create(
            ValidationErrorType.UnmatchedConditionalStart,
            "Test message",
            "Paragraph 5");

        // Assert
        Assert.Equal(ValidationErrorType.UnmatchedConditionalStart, error.Type);
        Assert.Equal("Test message", error.Message);
        Assert.Equal("Paragraph 5", error.Location);
    }

    [Fact]
    public void ValidationError_Create_WithoutLocation_LocationIsNull()
    {
        // Act
        ValidationError error = ValidationError.Create(
            ValidationErrorType.InvalidPlaceholderSyntax,
            "Test message");

        // Assert
        Assert.Equal(ValidationErrorType.InvalidPlaceholderSyntax, error.Type);
        Assert.Equal("Test message", error.Message);
        Assert.Null(error.Location);
    }

    [Fact]
    public void ValidationError_Init_AllowsPropertyInitialization()
    {
        // Act
        ValidationError error = new ValidationError
        {
            Type = ValidationErrorType.UnmatchedLoopEnd,
            Message = "Custom message",
            Location = "Table row 3"
        };

        // Assert
        Assert.Equal(ValidationErrorType.UnmatchedLoopEnd, error.Type);
        Assert.Equal("Custom message", error.Message);
        Assert.Equal("Table row 3", error.Location);
    }

    #endregion

    #region ValidationErrorType Tests

    [Fact]
    public void ValidationErrorType_HasExpectedValues()
    {
        // Assert - Verify all expected enum values exist
        Assert.True(Enum.IsDefined(typeof(ValidationErrorType), ValidationErrorType.UnmatchedConditionalStart));
        Assert.True(Enum.IsDefined(typeof(ValidationErrorType), ValidationErrorType.UnmatchedConditionalEnd));
        Assert.True(Enum.IsDefined(typeof(ValidationErrorType), ValidationErrorType.UnmatchedLoopStart));
        Assert.True(Enum.IsDefined(typeof(ValidationErrorType), ValidationErrorType.UnmatchedLoopEnd));
        Assert.True(Enum.IsDefined(typeof(ValidationErrorType), ValidationErrorType.InvalidPlaceholderSyntax));
        Assert.True(Enum.IsDefined(typeof(ValidationErrorType), ValidationErrorType.MissingVariable));
        Assert.True(Enum.IsDefined(typeof(ValidationErrorType), ValidationErrorType.InvalidConditionalExpression));
    }

    [Fact]
    public void ValidationErrorType_CanBeUsedInSwitch()
    {
        // Arrange
        ValidationErrorType errorType = ValidationErrorType.MissingVariable;
        string result;

        // Act
        switch (errorType)
        {
            case ValidationErrorType.UnmatchedConditionalStart:
                result = "Conditional start";
                break;
            case ValidationErrorType.UnmatchedConditionalEnd:
                result = "Conditional end";
                break;
            case ValidationErrorType.UnmatchedLoopStart:
                result = "Loop start";
                break;
            case ValidationErrorType.UnmatchedLoopEnd:
                result = "Loop end";
                break;
            case ValidationErrorType.InvalidPlaceholderSyntax:
                result = "Invalid syntax";
                break;
            case ValidationErrorType.MissingVariable:
                result = "Missing variable";
                break;
            case ValidationErrorType.InvalidConditionalExpression:
                result = "Invalid expression";
                break;
            default:
                result = "Unknown";
                break;
        }

        // Assert
        Assert.Equal("Missing variable", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidationResult_WithEmptyPlaceholders_IsValid()
    {
        // Act
        ValidationResult result = ValidationResult.Success(Array.Empty<string>());

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.AllPlaceholders);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidationResult_WithNullMissingVariables_TreatsAsEmpty()
    {
        // Arrange
        List<string> placeholders = new List<string> { "Name" };

        // Act
        ValidationResult result = ValidationResult.Success(placeholders, missingVariables: null);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void ValidationError_WithEmptyMessage_AllowsIt()
    {
        // Act
        ValidationError error = ValidationError.Create(
            ValidationErrorType.InvalidPlaceholderSyntax,
            string.Empty);

        // Assert
        Assert.Equal(string.Empty, error.Message);
    }

    #endregion
}
