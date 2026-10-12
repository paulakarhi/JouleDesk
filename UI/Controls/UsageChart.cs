using System.Drawing.Drawing2D;
using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>
/// Line chart of the energy recorded in one month, one point per day, in the style of the live power chart.
/// The line runs from the first to the last day with recordings; when that is today, the last stretch is dashed
/// because the day is not over. Pointing at the chart marks the nearest day and shows its date, energy and cost
/// in a bubble above it.
/// </summary>
internal sealed class UsageChart : ChartControl<DayUsage>
{
    private const int DayLabelWidth = 24;
    private const int DotRadius = 3;
    private const int HoverRadius = 5;
    private const float MinimumSlotForDots = 14;
    private const double MinimumAxisRange = 0.5;
    private const string Heading = "DAILY ENERGY";

    private MonthUsage? month;

    public UsageChart() => AccessibleName = Localization.Translate(Heading);

    /// <summary>The month to show.</summary>
    public MonthUsage? Month
    {
        get => month;
        set
        {
            month = value;
            ClearHover();
            AccessibleDescription = Summary();
            Invalidate();
        }
    }

    /// <summary>The plotted day nearest to a point of the control, if the point is inside the plot.</summary>
    internal override DayUsage? ItemAt(Point location)
    {
        var plot = Plot;
        if (!HasData || !plot.Contains(location))
            return null;

        var index = Math.Min(month!.Days.Count - 1, (location.X - plot.Left) * month.Days.Count / plot.Width);
        var day = month.Days[index];
        return month.Recorded.Contains(day) ? day : null;
    }

    /// <summary>Where the bubble for a day goes: centred above its point and kept inside the control.</summary>
    internal Rectangle TooltipBounds(DayUsage day)
    {
        var plot = Plot;
        var pointTop = AxisFor(month!).YOf(day.Kwh, plot) - HoverRadius;
        return ChartBubble.Place(ChartBubble.Measure(TooltipLines(day)), Size, CenterOf(day, plot), pointTop);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintCard(g);
        PaintHeader(g, Heading, month is null ? "" : month.Month.ToString("MMMM yyyy"), Summary());

        if (!HasData)
        {
            PaintEmptyMessage(g, "No consumption recorded this month");
            return;
        }

        var axis = AxisFor(month!);
        axis.PaintGrid(g, Plot, "kWh");
        PaintDayLabels(g);
        PaintCurve(g, axis);
        if (Hovered is not null)
        {
            PaintHover(g, axis, Hovered);
            ChartBubble.Paint(g, TooltipBounds(Hovered), CenterOf(Hovered, Plot), TooltipLines(Hovered));
        }
    }

    private bool HasData => month is { Kwh: > 0 };

    // The first day and every fifth day are numbered; a number that would touch the previous one is left out.
    private void PaintDayLabels(Graphics g)
    {
        var plot = Plot;
        var lastLabelEnd = int.MinValue;
        foreach (var day in month!.Days)
        {
            var x = (int)CenterOf(day, plot);
            if ((day.Date.Day != 1 && day.Date.Day % 5 != 0) || x - DayLabelWidth / 2 < lastLabelEnd)
                continue;

            g.DrawLabel(day.Date.Day.ToString(), new Rectangle(x - DayLabelWidth / 2, plot.Bottom + 8, DayLabelWidth, 20),
                TextStyles.Axis with { Align = TextAlign.Center });
            lastLabelEnd = x + DayLabelWidth / 2;
        }
    }

    // A dot marks each day when there is room for them.
    private void PaintCurve(Graphics g, ChartAxis axis)
    {
        var plot = Plot;
        var points = month!.Recorded.Select(day => PointOf(day, axis, plot)).ToArray();

        // A day that is still going on is not complete, so the stretch leading to it is dashed.
        ChartCurve.Paint(g, plot, points, dashLastStretch: month.Recorded[^1].Date == FinnishTime.Today);

        if (points.Length == 1 || plot.Width / (float)month.Days.Count >= MinimumSlotForDots)
        {
            using var dot = new SolidBrush(Accent);
            foreach (var point in points)
                g.FillEllipse(dot, point.X - DotRadius, point.Y - DotRadius, DotRadius * 2, DotRadius * 2);
        }
    }

    // A guide down to the axis and a larger dot with a ring in the colour of the chart.
    private void PaintHover(Graphics g, ChartAxis axis, DayUsage day)
    {
        var plot = Plot;
        var point = PointOf(day, axis, plot);

        using var guide = new Pen(Color.FromArgb(150, Foreground)) { DashStyle = DashStyle.Dash };
        g.DrawLine(guide, point.X, point.Y, point.X, plot.Bottom);

        using var ring = new SolidBrush(Surface);
        using var dot = new SolidBrush(Accent);
        g.FillEllipse(ring, point.X - HoverRadius - 2, point.Y - HoverRadius - 2, (HoverRadius + 2) * 2, (HoverRadius + 2) * 2);
        g.FillEllipse(dot, point.X - HoverRadius, point.Y - HoverRadius, HoverRadius * 2, HoverRadius * 2);
    }

    private PointF PointOf(DayUsage day, ChartAxis axis, Rectangle plot) => new(CenterOf(day, plot), axis.YOf(day.Kwh, plot));

    private float CenterOf(DayUsage day, Rectangle plot) =>
        plot.Left + (day.Date.Day - 0.5f) * plot.Width / month!.Days.Count;

    private static ChartAxis AxisFor(MonthUsage month) =>
        ChartAxis.For(0, month.Days.Max(day => day.Kwh), MinimumAxisRange);

    private static IReadOnlyList<(string Text, TextStyle Style)> TooltipLines(DayUsage day)
    {
        var partial = day.MissingKwh > 0;
        var lines = new List<(string, TextStyle)>
        {
            (day.Date.ToString("dddd d.M."), new TextStyle(9, SecondaryText)),
            ($"{day.Kwh:F2} kWh", new TextStyle(12, Foreground, Bold: true)),
            (DisplayFormat.Euros(day.Euros, partial, 2), new TextStyle(9, SecondaryText))
        };
        if (partial)
            lines.Add((Localization.Format("Price missing for {0} kWh", $"{day.MissingKwh:F2}"), new TextStyle(8, Warning)));
        return lines;
    }

    private string Summary()
    {
        if (month?.Highest is not { } highest)
            return "";

        return Localization.Format("Total {0} kWh · cost {1} · highest {2} kWh on {3}",
            $"{month.Kwh:F2}", DisplayFormat.Euros(month.Euros, month.MissingKwh > 0, 2), $"{highest.Kwh:F2}", highest.Date.ToString("d.M."));
    }
}
