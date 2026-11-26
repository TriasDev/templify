// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>A page size (portrait).</summary>
public sealed record PageSize
{
    /// <summary>Initializes a page size.</summary>
    /// <param name="width">The width; greater than zero.</param>
    /// <param name="height">The height; greater than zero.</param>
    public PageSize(Length width, Length height)
    {
        if (width <= Length.Zero || height <= Length.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "A page has a positive width and height.");
        }

        Width = width;
        Height = height;
    }

    /// <summary>Gets the width.</summary>
    public Length Width { get; }

    /// <summary>Gets the height.</summary>
    public Length Height { get; }

    /// <summary>Gets A4, 210 × 297 mm.</summary>
    public static PageSize A4 { get; } = new PageSize(210.Mm(), 297.Mm());

    /// <summary>Gets A5, 148 × 210 mm.</summary>
    public static PageSize A5 { get; } = new PageSize(148.Mm(), 210.Mm());

    /// <summary>Gets US Letter, 8.5 × 11 in.</summary>
    public static PageSize Letter { get; } = new PageSize(612.Pt(), 792.Pt());

    /// <summary>Gets US Legal, 8.5 × 14 in.</summary>
    public static PageSize Legal { get; } = new PageSize(612.Pt(), 1008.Pt());
}

/// <summary>
/// Describes the page: size, margins, header and footer. Valid only inside the lambda passed to
/// <see cref="TemplateComposer.Page"/>. Without it a template is A4 with 25 mm margins.
/// </summary>
public sealed class PageComposer
{
    private readonly BuildContext _context;
    private readonly PageSetupBuilder _setup;
    private bool _isOpen = true;

    internal PageComposer(BuildContext context, PageSetupBuilder setup)
    {
        _context = context;
        _setup = setup;
    }

    /// <summary>Sets the page size.</summary>
    /// <param name="size">The size, for example <see cref="PageSize.A4"/>.</param>
    /// <returns>This composer.</returns>
    public PageComposer Size(PageSize size)
    {
        EnsureOpen();
        _setup.Size = size ?? throw new ArgumentNullException(nameof(size));
        return this;
    }

    /// <summary>Sets all four margins: the distances from the page edges to the body text.</summary>
    /// <param name="top">The top margin.</param>
    /// <param name="right">The right margin.</param>
    /// <param name="bottom">The bottom margin.</param>
    /// <param name="left">The left margin.</param>
    /// <returns>This composer.</returns>
    public PageComposer Margins(Length top, Length right, Length bottom, Length left)
    {
        EnsureOpen();
        _setup.Top = StyleRanges.NotNegative(top, nameof(top));
        _setup.Right = StyleRanges.NotNegative(right, nameof(right));
        _setup.Bottom = StyleRanges.NotNegative(bottom, nameof(bottom));
        _setup.Left = StyleRanges.NotNegative(left, nameof(left));
        return this;
    }

    /// <summary>Sets the same margin on all four sides.</summary>
    /// <param name="all">The margin.</param>
    /// <returns>This composer.</returns>
    public PageComposer Margins(Length all) => Margins(all, all, all, all);

    /// <summary>Sets the distances from the top edge to the header and from the bottom edge to the footer.</summary>
    /// <param name="header">The header distance; at most the top margin.</param>
    /// <param name="footer">The footer distance; at most the bottom margin.</param>
    /// <returns>This composer.</returns>
    public PageComposer HeaderFooterDistance(Length header, Length footer)
    {
        EnsureOpen();
        _setup.HeaderDistance = StyleRanges.NotNegative(header, nameof(header));
        _setup.FooterDistance = StyleRanges.NotNegative(footer, nameof(footer));
        return this;
    }

    /// <summary>Describes the header of every page (and of the first page, unless <see cref="FirstPage"/> sets one).</summary>
    /// <param name="header">Describes the header's blocks: paragraphs, values, conditions, page numbers.</param>
    /// <returns>This composer.</returns>
    public PageComposer Header(Action<BlockComposer> header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureOpen();
        _setup.Header = BlockComposer.Compose(_context, null, header);
        return this;
    }

    /// <summary>Describes the footer of every page (and of the first page, unless <see cref="FirstPage"/> sets one).</summary>
    /// <param name="footer">Describes the footer's blocks.</param>
    /// <returns>This composer.</returns>
    public PageComposer Footer(Action<BlockComposer> footer)
    {
        ArgumentNullException.ThrowIfNull(footer);
        EnsureOpen();
        _setup.Footer = BlockComposer.Compose(_context, null, footer);
        return this;
    }

    /// <summary>
    /// Gives the first page its own header and footer, for example none on a cover page. What the first page does not
    /// set, it takes from the default header and footer.
    /// </summary>
    /// <param name="firstPage">Describes the first page's header and footer.</param>
    /// <returns>This composer.</returns>
    public PageComposer FirstPage(Action<FirstPageComposer> firstPage)
    {
        ArgumentNullException.ThrowIfNull(firstPage);
        EnsureOpen();
        _setup.DistinctFirstPage = true;
        FirstPageComposer composer = new FirstPageComposer(_context, _setup);
        firstPage(composer);
        composer.Close();
        return this;
    }

    internal void Close() => _isOpen = false;

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException("A page composer was used outside the lambda it was passed to.");
        }
    }
}

/// <summary>Describes the first page's header and footer. Valid only inside the lambda it is passed to.</summary>
public sealed class FirstPageComposer
{
    private readonly BuildContext _context;
    private readonly PageSetupBuilder _setup;
    private bool _isOpen = true;

    internal FirstPageComposer(BuildContext context, PageSetupBuilder setup)
    {
        _context = context;
        _setup = setup;
    }

    /// <summary>Describes the first page's header.</summary>
    /// <param name="header">Describes the header's blocks.</param>
    /// <returns>This composer.</returns>
    public FirstPageComposer Header(Action<BlockComposer> header)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureOpen();
        _setup.FirstHeader = BlockComposer.Compose(_context, null, header);
        return this;
    }

    /// <summary>Describes the first page's footer.</summary>
    /// <param name="footer">Describes the footer's blocks.</param>
    /// <returns>This composer.</returns>
    public FirstPageComposer Footer(Action<BlockComposer> footer)
    {
        ArgumentNullException.ThrowIfNull(footer);
        EnsureOpen();
        _setup.FirstFooter = BlockComposer.Compose(_context, null, footer);
        return this;
    }

    /// <summary>Leaves the first page without a header.</summary>
    /// <returns>This composer.</returns>
    public FirstPageComposer NoHeader()
    {
        EnsureOpen();
        _setup.FirstHeader = new List<Block>();
        return this;
    }

    /// <summary>Leaves the first page without a footer.</summary>
    /// <returns>This composer.</returns>
    public FirstPageComposer NoFooter()
    {
        EnsureOpen();
        _setup.FirstFooter = new List<Block>();
        return this;
    }

    internal void Close() => _isOpen = false;

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException("A first-page composer was used outside the lambda it was passed to.");
        }
    }
}

/// <summary>Collects the page setup while it is composed.</summary>
internal sealed class PageSetupBuilder
{
    public PageSize Size { get; set; } = PageSize.A4;

    public Length Top { get; set; } = 25.Mm();

    public Length Right { get; set; } = 25.Mm();

    public Length Bottom { get; set; } = 25.Mm();

    public Length Left { get; set; } = 25.Mm();

    public Length HeaderDistance { get; set; } = 12.5.Mm();

    public Length FooterDistance { get; set; } = 12.5.Mm();

    public List<Block>? Header { get; set; }

    public List<Block>? Footer { get; set; }

    public bool DistinctFirstPage { get; set; }

    public List<Block>? FirstHeader { get; set; }

    public List<Block>? FirstFooter { get; set; }

    public PageSetup ToSetup()
    {
        return new PageSetup(
            Size, Top, Right, Bottom, Left,
            Length.Min(HeaderDistance, Top),
            Length.Min(FooterDistance, Bottom),
            Header,
            Footer,
            DistinctFirstPage,
            DistinctFirstPage ? FirstHeader ?? Header : null,
            DistinctFirstPage ? FirstFooter ?? Footer : null);
    }
}
