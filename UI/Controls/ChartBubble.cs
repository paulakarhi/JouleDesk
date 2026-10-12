namespace JouleDesk;

/// <summary>The bubble a bar chart shows above the bar under the pointer: a few centred lines of text with a small pointer.</summary>
internal static class ChartBubble
{
    private const int Padding = 14;
    private const int VerticalPadding = 9;
    private const int LineGap = 2;
    private const int Pointer = 6;
    private const int Margin = 4;

    public static Size Measure(IReadOnlyList<(string Text, TextStyle Style)> lines)
    {
        var width = lines.Max(line => TextRenderer.MeasureText(line.Text, Theme.GetFont(line.Style.Size, line.Style.Bold), Size.Empty, TextFormatFlags.NoPadding).Width);
        var height = lines.Sum(line => LineHeight(line.Style)) + LineGap * (lines.Count - 1);
        return new Size(width + Padding * 2, height + VerticalPadding * 2);
    }

    /// <summary>
    /// Centres the bubble above the point <paramref name="anchorTop"/> and keeps it inside the chart.
    /// When the anchor is too high for the bubble to fit above it, the bubble overlaps the chart's heading instead.
    /// </summary>
    public static Rectangle Place(Size bubble, Size chart, float centerX, float anchorTop)
    {
        var x = Math.Clamp(centerX - bubble.Width / 2f, Margin, Math.Max(Margin, chart.Width - bubble.Width - Margin));
        var y = Math.Max(Margin, anchorTop - bubble.Height - Pointer - 2);
        return new Rectangle((int)x, (int)y, bubble.Width, bubble.Height);
    }

    public static void Paint(Graphics g, Rectangle bounds, float centerX, IReadOnlyList<(string Text, TextStyle Style)> lines)
    {
        g.DrawBox(bounds, Theme.HoverSurface, Theme.Border, 8);

        var pointerX = (int)Math.Clamp(centerX, bounds.Left + 12, bounds.Right - 12);
        using var fill = new SolidBrush(Theme.HoverSurface);
        g.FillPolygon(fill, new[]
        {
            new Point(pointerX - Pointer, bounds.Bottom - 1),
            new Point(pointerX + Pointer, bounds.Bottom - 1),
            new Point(pointerX, bounds.Bottom + Pointer)
        });

        var y = bounds.Y + VerticalPadding;
        foreach (var (text, style) in lines)
        {
            var height = LineHeight(style);
            g.DrawLabel(text, new Rectangle(bounds.X, y, bounds.Width, height), style with { Align = TextAlign.Center });
            y += height + LineGap;
        }
    }

    private static int LineHeight(TextStyle style) =>
        TextRenderer.MeasureText("Ag", Theme.GetFont(style.Size, style.Bold), Size.Empty, TextFormatFlags.NoPadding).Height + 2;
}
