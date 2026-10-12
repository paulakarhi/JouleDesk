using System.Drawing.Drawing2D;
using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>
/// Bar chart of one day's electricity prices, one bar per price interval. The colour of a bar runs from mint for the
/// cheapest intervals of the day to coral for the dearest. A dashed line marks the average, and on the current day
/// another marks now and earlier intervals fade. Pointing at a bar shows its time and price in a bubble above it.
/// </summary>
internal sealed class PriceChart : ChartControl<PriceBar>
{
    private const int HourLabelEvery = 3;
    private const int HourLabelWidth = 30;
    private const int BarRadius = 2;
    private const double PastFade = 0.5;
    private const double BottomFade = 0.4;
    private const double HoverLightening = 0.25;

    private DayPrices? day;
    private string title = "SPOT PRICE";

    public PriceChart() => AccessibleName = Localization.Translate(title);

    /// <summary>Heading of the chart as a translation key.</summary>
    public string Title
    {
        get => title;
        set
        {
            title = value;
            AccessibleName = Localization.Translate(title);
            Invalidate();
        }
    }

    /// <summary>The day to show. Bars are drawn with prices in cents per kWh as given.</summary>
    public DayPrices? Day
    {
        get => day;
        set
        {
            day = value;
            ClearHover();
            AccessibleDescription = Summary();
            Invalidate();
        }
    }

    internal override PriceBar? ItemAt(Point location)
    {
        var plot = Plot;
        if (day is null || !plot.Contains(location))
            return null;

        var instant = day.Start + (day.End - day.Start) * ((location.X - plot.Left) / (double)plot.Width);
        return day.Bars.FirstOrDefault(bar => bar.Start <= instant && instant < bar.End);
    }

    /// <summary>Where the bubble for a bar goes: centred above the bar and kept inside the control.</summary>
    internal Rectangle TooltipBounds(PriceBar bar)
    {
        var plot = Plot;
        var axis = AxisFor(day!);
        var barTop = Math.Min(axis.YOf(bar.Cents, plot), axis.YOf(0, plot));
        return ChartBubble.Place(ChartBubble.Measure(TooltipLines(bar)), Size, CenterOf(bar, plot), barTop);
    }

    /// <summary>The colour of a price within a day: mint at the lowest price, amber in the middle and coral at the highest.</summary>
    internal static Color LevelColor(double cents, double lowest, double highest)
    {
        if (highest - lowest < 0.01)
            return Accent;

        var position = Math.Clamp((cents - lowest) / (highest - lowest), 0, 1);
        return position < 0.5 ? Blend(Accent, Warning, position * 2) : Blend(Warning, Expensive, (position - 0.5) * 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintCard(g);
        PaintHeader(g, title, day is null ? "" : Localization.Translate(DayLabel(day.Date)), Summary());

        if (day is null || day.Bars.Count == 0)
        {
            PaintEmptyMessage(g, "No prices for this day yet");
            return;
        }

        var axis = AxisFor(day);
        axis.PaintGrid(g, Plot, "ct/kWh");
        PaintHourLabels(g);
        PaintBars(g, axis);
        PaintReferenceLines(g, axis);
        if (Hovered is not null)
            PaintTooltip(g, Hovered);
    }

    // Labels sit at every third clock hour that exists on this day. When the clocks change a day has
    // 23 or 25 hours, so a label is skipped (spring) or only the first of a repeated hour is drawn (autumn).
    private void PaintHourLabels(Graphics g)
    {
        var plot = Plot;
        using var gridPen = new Pen(Border);
        var lastLabelEnd = int.MinValue;
        var hours = (int)(day!.End - day.Start).TotalHours;
        for (var elapsed = 0; elapsed < hours; elapsed++)
        {
            var instant = day.Start.AddHours(elapsed);
            var local = FinnishTime.ToLocal(instant);
            var x = (int)XOf(instant, plot);
            if (local.Hour % HourLabelEvery != 0 || x - HourLabelWidth / 2 < lastLabelEnd)
                continue;

            if (elapsed > 0)
                g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
            g.DrawLabel(local.Hour.ToString("00"), new Rectangle(x - HourLabelWidth / 2, plot.Bottom + 8, HourLabelWidth, 20),
                TextStyles.Axis with { Align = TextAlign.Center });
            lastLabelEnd = x + HourLabelWidth / 2;
        }
    }

    private void PaintBars(Graphics g, ChartAxis axis)
    {
        var plot = Plot;
        var now = DateTimeOffset.UtcNow;
        var isToday = day!.Date == FinnishTime.Today;
        var baseline = axis.YOf(0, plot);
        var (lowest, highest) = (day.Lowest!.Cents, day.Highest!.Cents);

        foreach (var bar in day.Bars)
        {
            var left = (int)Math.Round(XOf(bar.Start, plot));
            var right = (int)Math.Round(XOf(bar.End, plot));
            var span = right - left;
            var width = Math.Max(1, span - (span > 3 ? Math.Clamp(span / 8, 1, 3) : 0));
            var valueY = axis.YOf(bar.Cents, plot);
            var rectangle = new Rectangle(left, (int)Math.Min(valueY, baseline), width, Math.Max(1, (int)Math.Abs(valueY - baseline)));

            var color = LevelColor(bar.Cents, lowest, highest);
            if (bar == Hovered)
                color = Blend(color, Color.White, HoverLightening);
            else if (isToday && bar.End <= now)
                color = Blend(color, Surface, PastFade);
            FillBar(g, rectangle, color, roundedAtBottom: bar.Cents < 0);
        }
    }

    // The average, and on the current day the moment of now, as thin dashed lines across the bars.
    private void PaintReferenceLines(Graphics g, ChartAxis axis)
    {
        var plot = Plot;
        using var pen = new Pen(Color.FromArgb(150, Foreground)) { DashStyle = DashStyle.Dash };

        if (day!.Average is { } average)
        {
            var y = (int)axis.YOf(average, plot);
            g.DrawLine(pen, plot.Left, y, plot.Right, y);
        }

        var now = DateTimeOffset.UtcNow;
        if (day.Start <= now && now < day.End)
        {
            var x = (int)XOf(now, plot);
            g.DrawLine(pen, x, plot.Top, x, plot.Bottom);
        }
    }

    private void PaintTooltip(Graphics g, PriceBar bar) =>
        ChartBubble.Paint(g, TooltipBounds(bar), CenterOf(bar, Plot), TooltipLines(bar));

    private float XOf(DateTimeOffset instant, Rectangle plot) =>
        plot.Left + (float)((instant - day!.Start) / (day.End - day.Start)) * plot.Width;

    private float CenterOf(PriceBar bar, Rectangle plot) => (XOf(bar.Start, plot) + XOf(bar.End, plot)) / 2;

    // Prices below one cent still need an axis, so the scale spans at least one cent.
    private static ChartAxis AxisFor(DayPrices day) => ChartAxis.For(day.Lowest!.Cents, day.Highest!.Cents, minimumRange: 1);

    private static IReadOnlyList<(string Text, TextStyle Style)> TooltipLines(PriceBar bar) => new[]
    {
        ($"{FinnishTime.ToLocal(bar.Start).ToString(DisplayFormat.Time)}–{FinnishTime.ToLocal(bar.End).ToString(DisplayFormat.Time)}", new TextStyle(9, SecondaryText)),
        ($"{bar.Cents:F2} {Localization.Translate("ct/kWh")}", new TextStyle(12, Foreground, Bold: true))
    };

    // A bar is a vertical gradient, strongest at its tip, with the tip rounded.
    private static void FillBar(Graphics g, Rectangle bar, Color color, bool roundedAtBottom)
    {
        using var path = BarShape(bar, roundedAtBottom);
        using var brush = new LinearGradientBrush(
            new Rectangle(bar.X, bar.Y, bar.Width, Math.Max(1, bar.Height)), color, Blend(color, Surface, BottomFade), LinearGradientMode.Vertical);
        g.FillPath(brush, path);
    }

    private static GraphicsPath BarShape(Rectangle bar, bool roundedAtBottom)
    {
        var path = new GraphicsPath();
        var radius = Math.Min(BarRadius, Math.Min(bar.Width, bar.Height) / 2);
        if (radius < 1)
        {
            path.AddRectangle(bar);
            return path;
        }

        var diameter = radius * 2;
        if (roundedAtBottom)
        {
            path.AddLine(bar.Left, bar.Top, bar.Right, bar.Top);
            path.AddArc(bar.Right - diameter, bar.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bar.Left, bar.Bottom - diameter, diameter, diameter, 90, 90);
        }
        else
        {
            path.AddArc(bar.Left, bar.Top, diameter, diameter, 180, 90);
            path.AddArc(bar.Right - diameter, bar.Top, diameter, diameter, 270, 90);
            path.AddLine(bar.Right, bar.Bottom, bar.Left, bar.Bottom);
        }
        path.CloseFigure();
        return path;
    }

    private static Color Blend(Color from, Color to, double amount) => Color.FromArgb(
        (int)Math.Round(from.R + (to.R - from.R) * amount),
        (int)Math.Round(from.G + (to.G - from.G) * amount),
        (int)Math.Round(from.B + (to.B - from.B) * amount));

    private string Summary()
    {
        if (day is not { Average: { } average, Lowest: { } lowest, Highest: { } highest })
            return "";

        string TimeOf(PriceBar bar) => FinnishTime.ToLocal(bar.Start).ToString(DisplayFormat.Time);
        return Localization.Format("Average {0} · lowest {1} at {2} · highest {3} at {4}",
            $"{average:F2}", $"{lowest.Cents:F2}", TimeOf(lowest), $"{highest.Cents:F2}", TimeOf(highest));
    }

    private static string DayLabel(DateTime date)
    {
        var relative = (date - FinnishTime.Today).Days switch
        {
            -1 => "Yesterday",
            0 => "Today",
            1 => "Tomorrow",
            _ => null
        };
        return relative is null ? date.ToString("dddd d.M.") : $"{Localization.Translate(relative)} · {date:d.M.}";
    }
}
