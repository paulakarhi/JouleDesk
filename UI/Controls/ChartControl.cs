using System.Drawing.Drawing2D;
using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>
/// The shared frame of the app's charts: a card with a heading, a summary line and a plot area, and hover tracking
/// for the item (a bar or a day) that a chart finds under the pointer.
/// </summary>
internal abstract class ChartControl<TItem> : Control
    where TItem : class
{
    protected const int HeaderInset = 20;
    private const int PlotLeft = 52;
    private const int PlotRight = 20;
    private const int PlotTop = 100;
    private const int PlotBottomInset = 40;

    protected ChartControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.Chart;
    }

    /// <summary>The item under the pointer, or null.</summary>
    protected TItem? Hovered { get; private set; }

    protected Rectangle Plot => new(PlotLeft, PlotTop, Math.Max(1, Width - PlotLeft - PlotRight), Math.Max(1, Height - PlotTop - PlotBottomInset));

    /// <summary>The item under a point of the control, if any.</summary>
    internal abstract TItem? ItemAt(Point location);

    /// <summary>Forgets the hovered item, for when the chart gets new data.</summary>
    protected void ClearHover() => Hovered = null;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var item = ItemAt(e.Location);
        if (!Equals(item, Hovered))
        {
            Hovered = item;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (Hovered is not null)
        {
            Hovered = null;
            Invalidate();
        }
    }

    protected void PaintCard(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawBox(new Rectangle(0, 0, Width - 1, Height - 1), Surface);
    }

    /// <summary>
    /// The heading and a label at the right edge share the first line, and the summary follows below.
    /// The label only takes the room its text needs, so a long heading keeps the rest.
    /// </summary>
    protected void PaintHeader(Graphics g, string heading, string side, string summary)
    {
        var width = Width - HeaderInset * 2;
        var sideWidth = side.Length == 0 ? 0 : TextRenderer.MeasureText(side, GetFont(TextStyles.Caption.Size, bold: true)).Width + 8;

        g.DrawLabel(heading, new Rectangle(HeaderInset, 17, width - sideWidth, 23), TextStyles.Caption);
        if (side.Length > 0)
            g.DrawLabel(side, new Rectangle(HeaderInset + width - sideWidth, 17, sideWidth, 23), TextStyles.Caption with { Align = TextAlign.Right });
        if (summary.Length > 0)
            g.DrawLabel(summary, new Rectangle(HeaderInset, 44, width, 40), TextStyles.Hint);
    }

    protected void PaintEmptyMessage(Graphics g, string message) =>
        g.DrawLabel(message, Plot with { Height = 40 }, new TextStyle(11, SecondaryText, Wrap: true));
}
