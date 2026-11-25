// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>Names a box style: border, background and padding of a framed box.</summary>
public sealed record BoxStyleKey
{
    /// <summary>Initializes a box style key.</summary>
    /// <param name="id">Letters, digits, <c>_</c> and <c>-</c>, starting with a letter or <c>_</c>.</param>
    public BoxStyleKey(string id)
    {
        Id = StyleKeyRules.ValidateId(id, nameof(id));
    }

    /// <summary>Gets the id.</summary>
    public string Id { get; }
}

/// <summary>Sets a box style's border, background and padding.</summary>
public sealed class BoxStyleComposer
{
    private readonly StyleSheetComposer _owner;
    private readonly BoxStyleDefinition _style;

    internal BoxStyleComposer(StyleSheetComposer owner, BoxStyleDefinition style)
    {
        _owner = owner;
        _style = style;
    }

    /// <summary>Sets the border around the box.</summary>
    /// <param name="width">The line width; more than 0 and at most 6 pt.</param>
    /// <param name="color">The colour.</param>
    /// <returns>This composer.</returns>
    public BoxStyleComposer Border(Length width, Color color)
    {
        _owner.EnsureOpen();
        if (width <= Length.Zero || width > 6.Pt())
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A line is more than 0 and at most 6 pt wide.");
        }

        _style.Border = new BorderLine(width, color);
        return this;
    }

    /// <summary>Sets the background.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>This composer.</returns>
    public BoxStyleComposer Background(Color color)
    {
        _owner.EnsureOpen();
        _style.Background = color;
        return this;
    }

    /// <summary>Sets the same padding on all sides.</summary>
    /// <param name="all">The padding.</param>
    /// <returns>This composer.</returns>
    public BoxStyleComposer Padding(Length all) => Padding(all, all);

    /// <summary>Sets the padding between the border and the content.</summary>
    /// <param name="vertical">Top and bottom.</param>
    /// <param name="horizontal">Left and right.</param>
    /// <returns>This composer.</returns>
    public BoxStyleComposer Padding(Length vertical, Length horizontal)
    {
        _owner.EnsureOpen();
        _style.PaddingVertical = StyleRanges.NotNegative(vertical, nameof(vertical));
        _style.PaddingHorizontal = StyleRanges.NotNegative(horizontal, nameof(horizontal));
        return this;
    }

    /// <summary>Sets the space after the box.</summary>
    /// <param name="space">The space.</param>
    /// <returns>This composer.</returns>
    public BoxStyleComposer SpaceAfter(Length space)
    {
        _owner.EnsureOpen();
        _style.SpaceAfter = StyleRanges.NotNegative(space, nameof(space));
        return this;
    }
}
