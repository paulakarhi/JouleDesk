using System.Drawing.Drawing2D;
using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>A line over a soft fill that fades towards the bottom of the plot, the style of the app's curve charts.</summary>
internal static class ChartCurve
{
    private const float LineWidth = 2.5f;

    /// <summary>Draws the curve through <paramref name="points"/>, which are ordered from left to right. The last stretch can be dashed.</summary>
    public static void Paint(Graphics g, Rectangle plot, PointF[] points, bool dashLastStretch = false)
    {
        if (points.Length < 2)
            return;

        using var area = new GraphicsPath();
        area.AddLines(points);
        area.AddLine(points[^1], new PointF(points[^1].X, plot.Bottom));
        area.AddLine(new PointF(points[^1].X, plot.Bottom), new PointF(points[0].X, plot.Bottom));
        area.CloseFigure();
        using var fill = new LinearGradientBrush(plot, ChartFillTop, ChartFillBottom, LinearGradientMode.Vertical);
        g.FillPath(fill, area);

        var solid = dashLastStretch ? points[..^1] : points;
        using var pen = new Pen(Accent, LineWidth) { LineJoin = LineJoin.Round };
        if (solid.Length > 1)
            g.DrawLines(pen, solid);
        if (dashLastStretch)
        {
            using var dashed = new Pen(Accent, LineWidth) { DashStyle = DashStyle.Dash };
            g.DrawLine(dashed, points[^2], points[^1]);
        }
    }
}
