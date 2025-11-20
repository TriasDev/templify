using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests;

public class PlaceholderReplacementOptionsTests
{
    [Fact]
    public void Constructor_SetsDefaultBehavior()
    {
        // Act
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();

        // Assert
        Assert.Equal(MissingVariableBehavior.LeaveUnchanged, options.MissingVariableBehavior);
    }

    [Fact]
    public void Init_AllowsSettingBehavior()
    {
        // Act
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty
        };

        // Assert
        Assert.Equal(MissingVariableBehavior.ReplaceWithEmpty, options.MissingVariableBehavior);
    }

    [Fact]
    public void Init_AllowsSettingThrowExceptionBehavior()
    {
        // Act
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException
        };

        // Assert
        Assert.Equal(MissingVariableBehavior.ThrowException, options.MissingVariableBehavior);
    }
}
