using System.Drawing.Drawing2D;

namespace JouleDesk;

internal static class GraphicsExtensions
{
    private const int DefaultRadius = 14;

    private const TextFormatFlags BaseTextFlags =
        TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform |
        TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

    /// <summary>Fills a rounded rectangle and optionally outlines it.</summary>
    public static void DrawBox(this Graphics g, Rectangle bounds, Color fill, Color? border = null, int radius = DefaultRadius)
    {
        using var path = RoundedRectangle(bounds, radius);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);
        if (border.HasValue)
        {
            using var pen = new Pen(border.Value);
            g.DrawPath(pen, path);
        }
    }

    /// <summary>
    /// Draws text inside <paramref name="bounds"/>, shortened with an ellipsis when it does not fit.
    /// The text is looked up in the translation table; text that is not a key is drawn as given.
    /// </summary>
    public static void DrawLabel(this Graphics g, string text, Rectangle bounds, TextStyle style)
    {
        var alignment = style.Align switch
        {
            TextAlign.Center => TextFormatFlags.HorizontalCenter,
            TextAlign.Right => TextFormatFlags.Right,
            _ => TextFormatFlags.Left
        };
        var flags = BaseTextFlags | alignment | (style.Wrap ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g, Localization.Translate(text), Theme.GetFont(style.Size, style.Bold), bounds, style.Color, flags);
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
