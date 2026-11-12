using System.Drawing.Drawing2D;
using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>
/// Bar chart of the average power of every minute in a stretch of one day, in the style of the price chart: the colour of
/// a bar runs from mint for the day's lowest power to coral for its highest. A day does not have room for a bar per
/// minute, so there a bar is a pixel column holding the highest minute under it; a short stretch such as an hour gives
/// every minute its own bar. Pointing at a bar shows its minute, power, energy and cost in a bubble above it.
/// </summary>
internal sealed class ConsumptionChart : ChartControl<MinuteUsage>
{
    private const double MinutePixelsForOwnBars = 3;
    private const double MinimumAxisWatts = 100;
    private const double HoverLightening = 0.25;
    private const int LabelHeight = 20;
    private static readonly TimeSpan WholeDayLabelStep = TimeSpan.FromHours(3);
    private static readonly TimeSpan HourLabelStep = TimeSpan.FromHours(1);
    private static readonly TimeSpan MinuteLabelStep = TimeSpan.FromMinutes(10);

    private readonly string title;
    private readonly string emptyMessage;
    private readonly List<(DateTimeOffset At, string Text)> timeLabels = new();

    private DayConsumption? day;
    private DateTimeOffset windowStart;
    private TimeSpan windowLength = UsageBuckets.Minute;
    private string side = "";
    private IReadOnlyList<MinuteUsage> windowMinutes = Array.Empty<MinuteUsage>();
    private ConsumptionSummary? summary;
    private DateTimeOffset? highlightStart;
    private TimeSpan highlightLength;

    /// <param name="title">Heading of the chart as a translation key.</param>
    /// <param name="emptyMessage">What to say when the stretch has no recorded usage, as a translation key.</param>
    public ConsumptionChart(string title, string emptyMessage)
    {
        this.title = title;
        this.emptyMessage = emptyMessage;
        AccessibleName = Localization.Translate(title);
    }

    /// <summary>
    /// Shows the stretch of <paramref name="length"/> that begins at <paramref name="start"/>.
    /// <paramref name="sideText"/> sits at the right end of the heading line.
    /// </summary>
    public void Show(DayConsumption? shownDay, DateTimeOffset start, TimeSpan length, string sideText)
    {
        day = shownDay;
        windowStart = start;
        windowLength = length;
        side = sideText;
        windowMinutes = shownDay?.MinutesBetween(start, start + length) ?? Array.Empty<MinuteUsage>();
        summary = windowMinutes.Count == 0 ? null : ConsumptionSummary.Of(windowMinutes);
        BuildTimeLabels();
        ClearHover();
        AccessibleDescription = SummaryText();
        Invalidate();
    }

    /// <summary>Shades a stretch of the chart, to show which part of the day another chart magnifies. Null removes the shade.</summary>
    public void Highlight(DateTimeOffset? start, TimeSpan length)
    {
        highlightStart = start;
        highlightLength = length;
        Invalidate();
    }

    private bool HasData => day is not null && windowMinutes.Count > 0;

    private double PixelsPerMinute => (double)Plot.Width * UsageBuckets.Minute.Ticks / windowLength.Ticks;

    /// <summary>The minute under a point of the control: the one under the pointer, or the highest of those under its pixel column.</summary>
    internal override MinuteUsage? ItemAt(Point location)
    {
        var plot = Plot;
        if (!HasData || !plot.Contains(location))
            return null;

        var from = InstantOf(location.X - plot.Left);
        if (PixelsPerMinute >= MinutePixelsForOwnBars)
            return day!.MinuteAt(from);
        return day!.MinutesBetween(from, InstantOf(location.X - plot.Left + 1)).MaxBy(minute => minute.Watts);
    }

    /// <summary>Where the bubble for a minute goes: centred above its bar and kept inside the control.</summary>
    internal Rectangle TooltipBounds(MinuteUsage minute)
    {
        var plot = Plot;
        var barTop = AxisFor(windowMinutes).YOf(minute.Watts, plot);
        return ChartBubble.Place(ChartBubble.Measure(TooltipLines(minute)), Size, CenterOf(minute, plot), barTop);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintCard(g);
        PaintHeader(g, title, side, SummaryText());

        if (!HasData)
        {
            PaintEmptyMessage(g, emptyMessage);
            return;
        }

        var plot = Plot;
        var axis = AxisFor(windowMinutes);
        axis.PaintGrid(g, plot, "W");
        PaintTimeLabels(g);
        PaintBars(g, axis);
        PaintHighlight(g);
        PaintNow(g);
        if (Hovered is not null)
            ChartBubble.Paint(g, TooltipBounds(Hovered), CenterOf(Hovered, plot), TooltipLines(Hovered));
    }

    private void PaintHighlight(Graphics g)
    {
        if (highlightStart is not { } start)
            return;

        var plot = Plot;
        var left = (int)Math.Clamp(XOf(start, plot), plot.Left, plot.Right);
        var right = (int)Math.Clamp(XOf(start + highlightLength, plot), plot.Left, plot.Right);
        using var shade = new SolidBrush(Color.FromArgb(40, Foreground));
        g.FillRectangle(shade, left, plot.Top, Math.Max(1, right - left), plot.Height);
    }

    private void PaintBars(Graphics g, ChartAxis axis)
    {
        var plot = Plot;
        var baseline = axis.YOf(0, plot);
        g.SmoothingMode = SmoothingMode.None;

        if (PixelsPerMinute >= MinutePixelsForOwnBars)
        {
            foreach (var minute in windowMinutes)
            {
                var left = (int)Math.Round(XOf(minute.Start, plot));
                var span = (int)Math.Round(XOf(minute.End, plot)) - left;
                PaintBar(g, minute, axis, baseline, left, Math.Max(1, span - (span > 4 ? 1 : 0)));
            }
        }
        else
        {
            foreach (var (x, minute) in TallestMinutePerColumn(plot))
                PaintBar(g, minute, axis, baseline, x, 1);
        }
        g.SmoothingMode = SmoothingMode.AntiAlias;
    }

    private void PaintBar(Graphics g, MinuteUsage minute, ChartAxis axis, float baseline, int left, int width)
    {
        var top = (int)Math.Min(axis.YOf(minute.Watts, Plot), baseline - 1);
        var color = ChartColors.Level(minute.Watts, day!.LowestWatts, day.HighestWatts);
        if (minute == Hovered)
            color = ChartColors.Blend(color, Color.White, HoverLightening);
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, left, top, width, (int)baseline - top);
    }

    // The minute drawn in each pixel column of the plot that has one: the highest of the minutes the column covers.
    private IEnumerable<(int X, MinuteUsage Minute)> TallestMinutePerColumn(Rectangle plot)
    {
        var tallest = new MinuteUsage?[plot.Width];
        foreach (var minute in windowMinutes)
        {
            var first = Math.Clamp((int)XOf(minute.Start, plot) - plot.Left, 0, plot.Width - 1);
            var last = Math.Clamp((int)XOf(minute.End - TimeSpan.FromTicks(1), plot) - plot.Left, 0, plot.Width - 1);
            for (var column = first; column <= last; column++)
            {
                if (tallest[column] is not { } current || minute.Watts > current.Watts)
                    tallest[column] = minute;
            }
        }

        for (var column = 0; column < tallest.Length; column++)
        {
            if (tallest[column] is { } minute)
                yield return (plot.Left + column, minute);
        }
    }

    // The moment of now, when it falls inside the chart, as a thin dashed line across the bars.
    private void PaintNow(Graphics g)
    {
        var now = DateTimeOffset.UtcNow;
        if (now < windowStart || now >= windowStart + windowLength)
            return;

        var plot = Plot;
        using var pen = new Pen(Color.FromArgb(150, Foreground)) { DashStyle = DashStyle.Dash };
        var x = (int)XOf(now, plot);
        g.DrawLine(pen, x, plot.Top, x, plot.Bottom);
    }

    private void PaintTimeLabels(Graphics g)
    {
        var plot = Plot;
        using var gridPen = new Pen(Border);
        var labelWidth = TextRenderer.MeasureText(timeLabels.Count == 0 ? "00" : timeLabels[0].Text, GetFont(TextStyles.Axis.Size)).Width + 8;
        var lastLabelEnd = int.MinValue;
        foreach (var (at, text) in timeLabels)
        {
            var x = (int)XOf(at, plot);
            if (x - labelWidth / 2 < lastLabelEnd)
                continue;

            if (at > windowStart)
                g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
            g.DrawLabel(text, new Rectangle(x - labelWidth / 2, plot.Bottom + 8, labelWidth, LabelHeight), TextStyles.Axis with { Align = TextAlign.Center });
            lastLabelEnd = x + labelWidth / 2;
        }
    }

    // Labels sit at round clock times: every third hour for a whole day, every hour for a few hours and every ten minutes below that.
    private void BuildTimeLabels()
    {
        timeLabels.Clear();
        var step = windowLength >= TimeSpan.FromHours(12) ? WholeDayLabelStep : windowLength >= TimeSpan.FromHours(3) ? HourLabelStep : MinuteLabelStep;
        var showsMinutes = step < TimeSpan.FromHours(1);
        var minutes = (int)windowLength.TotalMinutes;
        for (var offset = 0; offset < minutes; offset++)
        {
            var at = windowStart + UsageBuckets.Minute * offset;
            var local = FinnishTime.ToLocal(at);
            if ((int)local.TimeOfDay.TotalMinutes % (int)step.TotalMinutes != 0)
                continue;
            timeLabels.Add((at, showsMinutes ? local.ToString(DisplayFormat.Time) : local.Hour.ToString("00")));
        }
    }

    private DateTimeOffset InstantOf(int pixel) => windowStart + windowLength * (pixel / (double)Plot.Width);

    private float XOf(DateTimeOffset instant, Rectangle plot) =>
        plot.Left + (float)((instant - windowStart) / windowLength) * plot.Width;

    private float CenterOf(MinuteUsage minute, Rectangle plot) => (XOf(minute.Start, plot) + XOf(minute.End, plot)) / 2;

    private static ChartAxis AxisFor(IReadOnlyList<MinuteUsage> minutes) =>
        ChartAxis.For(0, minutes.Max(minute => minute.Watts), MinimumAxisWatts);

    private static List<(string Text, TextStyle Style)> TooltipLines(MinuteUsage minute)
    {
        var partial = minute.MissingKwh > 0;
        var cost = $"{minute.Euros * 100:F4}{(partial ? " *" : "")}";
        var lines = new List<(string, TextStyle)>
        {
            ($"{FinnishTime.ToLocal(minute.Start).ToString(DisplayFormat.Time)}–{FinnishTime.ToLocal(minute.End).ToString(DisplayFormat.Time)}",
                new TextStyle(9, SecondaryText)),
            ($"{minute.Watts:F1} W", new TextStyle(12, Foreground, Bold: true)),
            (Localization.Format("{0} Wh · {1} ct", $"{minute.WattHours:F3}", cost), new TextStyle(9, SecondaryText))
        };
        if (partial)
            lines.Add((Localization.Format("Price missing for {0} kWh", $"{minute.MissingKwh:F4}"), new TextStyle(8, Warning)));
        if (!minute.IsComplete)
            lines.Add((Localization.Format("Only {0} s of this minute recorded", $"{minute.Recorded.TotalSeconds:F0}"), new TextStyle(8, SecondaryText)));
        return lines;
    }

    private string SummaryText()
    {
        if (summary is not { AverageWatts: { } average, Peak: { } peak } known)
            return "";

        return Localization.Format("Total {0} kWh · cost {1} · average {2} W · peak {3} W at {4}",
            $"{known.Kwh:F3}", DisplayFormat.Euros(known.Euros, known.MissingKwh > 0, 3), $"{average:F1}", $"{peak.Watts:F1}",
            FinnishTime.ToLocal(peak.Start).ToString(DisplayFormat.Time));
    }
}
