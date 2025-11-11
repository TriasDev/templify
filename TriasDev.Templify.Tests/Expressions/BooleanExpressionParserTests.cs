using System.Reflection;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Expressions;

public class BooleanExpressionParserTests
{
    // Use reflection to access internal types
    private static readonly Type ParserType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.Expressions.BooleanExpressionParser")!;

    private static readonly Type ExpressionType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.Expressions.BooleanExpression")!;

    private object CreateParser()
    {
        return Activator.CreateInstance(ParserType)!;
    }

    private object? Parse(object parser, string text)
    {
        MethodInfo? parseMethod = ParserType.GetMethod("Parse");
        return parseMethod?.Invoke(parser, new object[] { text });
    }

    [Fact]
    public void Parse_WithSimpleVariable_ReturnsNull()
    {
        // Arrange - simple variables should not be parsed as expressions
        object parser = CreateParser();

        // Act
        object? result = Parse(parser, "IsActive");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Parse_WithAndExpression_ReturnsExpression()
    {
        // Arrange
        object parser = CreateParser();

        // Act
        object? result = Parse(parser, "(var1 and var2)");

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom(ExpressionType, result);
    }

    [Fact]
    public void Parse_WithOrExpression_ReturnsExpression()
    {
        // Arrange
        object parser = CreateParser();

        // Act
        object? result = Parse(parser, "(var1 or var2)");

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom(ExpressionType, result);
    }

    [Fact]
    public void Parse_WithNotExpression_ReturnsExpression()
    {
        // Arrange
        object parser = CreateParser();

        // Act
        object? result = Parse(parser, "(not IsActive)");

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom(ExpressionType, result);
    }

    [Fact]
    public void Parse_WithComparisonGreaterThan_ReturnsExpression()
    {
        // Arrange
        object parser = CreateParser();

        // Act
        object? result = Parse(parser, "(Count > 0)");

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom(ExpressionType, result);
    }

    [Fact]
    public void Parse_WithComparisonEquals_ReturnsExpression()
    {
        // Arrange
        object parser = CreateParser();

        // Act
        object? result = Parse(parser, "(Status == \"active\")");

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom(ExpressionType, result);
    }

    [Fact]
    public void Parse_WithNestedExpression_ReturnsExpression()
    {
        // Arrange
        object parser = CreateParser();

        // Act
        object? result = Parse(parser, "((var1 or var2) and var3)");

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom(ExpressionType, result);
    }

    [Fact]
    public void Parse_WithEmptyString_ReturnsNull()
    {
        // Arrange
        object parser = CreateParser();

        // Act
        object? result = Parse(parser, "");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Parse_WithWhitespace_ReturnsNull()
    {
        // Arrange
        object parser = CreateParser();

        // Act
        object? result = Parse(parser, "   ");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("(var1 and var2)")]
    [InlineData("(var1 or var2)")]
    [InlineData("(not var1)")]
    [InlineData("(Count > 5)")]
    [InlineData("(Count >= 5)")]
    [InlineData("(Count < 10)")]
    [InlineData("(Count <= 10)")]
    [InlineData("(Status == \"active\")")]
    [InlineData("(Status != \"inactive\")")]
    public void Parse_WithValidExpressions_ReturnsExpression(string expression)
    {
        // Arrange
        object parser = CreateParser();

        // Act
        object? result = Parse(parser, expression);

        // Assert
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("var1")]  // No parentheses
    [InlineData("IsActive")]  // Simple variable
    [InlineData("Customer.Name")]  // Nested property
    public void Parse_WithoutParentheses_ReturnsNull(string text)
    {
        // Arrange - expressions must start with parenthesis
        object parser = CreateParser();

        // Act
        object? result = Parse(parser, text);

        // Assert
        Assert.Null(result);
    }
}
