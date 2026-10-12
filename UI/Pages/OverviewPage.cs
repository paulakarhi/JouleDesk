using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>Live power and cost figures, today's and this month's totals, and the last ten minutes of power draw.</summary>
internal sealed class OverviewPage : PageView
{
    private const int ChartHeight = 220;
    private const int ChartPlotTopInset = 76;
    private const int MinimumAxisWatts = 100;

    public OverviewPage(IPageHost host)
        : base(host)
    {
    }

    public override string Title => "Your energy, in view.";

    public override string Subtitle => "Power from sensors. Costs from tracking.";

    public override int ContentHeight => Layout.ChartTop + 330;

    protected override void CreateControls()
    {
        AddButton(Monitor.Paused ? "Resume" : "Pause", ResponsiveLayout.ActionButton(0, 132), TogglePause);
        AddButton("Hide to tray", ResponsiveLayout.ActionButton(144, Math.Min(141, Layout.Width - 144)), Host.HideToTray);
    }

    protected override void PaintBody(Graphics g)
    {
        var layout = Layout;
        var settings = Store.Settings;
        var now = FinnishTime.Now;
        var today = Store.TotalsFor(now);
        var month = Store.TotalsForMonth(now);
        var spotNow = SpotPrices.At(Store.Prices, DateTimeOffset.UtcNow)?.PriceCents;
        var centsNow = TariffCalculator.Cents(spotNow, settings);
        var planName = TariffCalculator.Name(settings.PriceMode);

        string Cost(UsageTotals totals) => DisplayFormat.Euros(totals.Cost(settings), totals.MissingKwh(settings) > 0, 3);

        PaintMetric(g, layout.MetricCell(0), "WHOLE PC · ESTIMATE", DisplayFormat.Watts(Monitor.WallWatts), "Includes estimated PSU losses", highlight: true);
        PaintMetric(g, layout.MetricCell(1), "CPU · SENSOR", DisplayFormat.Watts(Monitor.CpuWatts), "CPU package average");
        PaintMetric(g, layout.MetricCell(2), "GPU · SENSOR", DisplayFormat.Watts(Monitor.GpuWatts), "NVIDIA driver power reading");
        PaintMetric(g, layout.MetricCell(3), "SELECTED PRICE NOW", centsNow.HasValue ? $"{centsNow:F2}" : "—",
            $"{Localization.Translate("ct/kWh")} · {Localization.Translate(planName)}");
        PaintMetric(g, layout.MetricCell(4), "TODAY · RECORDED", Cost(today), $"{today.Kwh:F3} kWh · {Localization.Translate("selected plan")}");
        PaintMetric(g, layout.MetricCell(5), "THIS MONTH", Cost(month), $"{month.Kwh:F3} kWh · {Localization.Translate("selected plan")}");

        var x = ResponsiveLayout.Margin;
        var chart = new Rectangle(x, layout.ChartTop, layout.Width, ChartHeight);
        g.DrawBox(chart, Surface);
        g.DrawLabel("POWER OVER TIME", new Rectangle(chart.X + 20, chart.Y + 17, chart.Width - 40, 23), TextStyles.Caption);
        g.DrawLabel("LAST 10 MINUTES", new Rectangle(chart.X + 20, chart.Y + 40, chart.Width - 40, 23), TextStyles.Axis with { Align = TextAlign.Right });
        PaintTrend(g, new Rectangle(chart.X + 52, chart.Y + ChartPlotTopInset, chart.Width - 77, chart.Height - 108));

        var rate = Monitor.WallWatts.HasValue && centsNow.HasValue
            ? Localization.Format("About {0} €/h now", $"{Monitor.WallWatts * centsNow / 100000:F4}")
            : Localization.Translate("Waiting for power or price data");
        g.DrawLabel($"{rate} · {Localization.Translate(planName)}", new Rectangle(x, chart.Bottom + 13, layout.Width, 40),
            new TextStyle(10, Accent, Wrap: true));

        var missing = today.MissingKwh(settings);
        var footnote = missing > 0
            ? Localization.Format("* Price missing for {0} kWh. Cost is partial.", $"{missing:F4}")
            : "Monitoring continues with the window closed. Displays are excluded.";
        g.DrawLabel(footnote, new Rectangle(x, chart.Bottom + 58, layout.Width, 48), TextStyles.Hint);
    }

    private void TogglePause()
    {
        Monitor.SetPaused(!Monitor.Paused);
        Rebuild();
    }

    private static void PaintMetric(Graphics g, Rectangle bounds, string label, string value, string hint, bool highlight = false)
    {
        g.DrawBox(bounds, highlight ? AccentSurface : Surface, highlight ? AccentBorder : null);
        g.DrawLabel(label, new Rectangle(bounds.X + 20, bounds.Y + 19, bounds.Width - 40, 22),
            TextStyles.Caption with { Color = highlight ? Accent : SecondaryText });
        g.DrawLabel(value, new Rectangle(bounds.X + 20, bounds.Y + 49, bounds.Width - 36, 56), new TextStyle(29, Foreground, Bold: true));
        g.DrawLabel(hint, new Rectangle(bounds.X + 20, bounds.Bottom - 39, bounds.Width - 38, 31), TextStyles.Hint);
    }

    // The curve plots the whole-PC estimate over the monitor's trend window, newest sample at the right edge.
    private void PaintTrend(Graphics g, Rectangle plot)
    {
        var trend = Monitor.Trend;
        var axis = ChartAxis.For(0, trend.Count == 0 ? 0 : trend.Max(point => point.Watts), MinimumAxisWatts);
        axis.PaintGrid(g, plot, "W");
        g.DrawLabel("−10 min", new Rectangle(plot.X, plot.Bottom + 7, 90, 22), TextStyles.Axis);
        g.DrawLabel("now", new Rectangle(plot.Right - 55, plot.Bottom + 7, 55, 22), TextStyles.Axis with { Align = TextAlign.Right });

        if (trend.Count < 2)
        {
            g.DrawLabel("The power chart builds as monitoring runs", new Rectangle(plot.X + 25, plot.Y + 28, plot.Width - 30, 35),
                new TextStyle(11, SecondaryText));
            return;
        }

        var newest = trend[^1].At;
        var windowSeconds = PowerMonitor.TrendWindow.TotalSeconds;
        var points = trend
            .Select(point => new PointF(plot.Right - (float)((newest - point.At).TotalSeconds / windowSeconds) * plot.Width, axis.YOf(point.Watts, plot)))
            .ToArray();
        ChartCurve.Paint(g, plot, points);
    }
}
