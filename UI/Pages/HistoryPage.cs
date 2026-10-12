using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>
/// Recorded energy: a curve of one month at a time, with month navigation, and below it the days of that month
/// priced with the selected plan. The full history can be exported as CSV.
/// </summary>
internal sealed class HistoryPage : PageView
{
    private UsageChart? chart;
    private ThemedButton? previousMonth;
    private ThemedButton? nextMonth;

    // Null follows the latest month with recorded energy, so the chart stays current as new days are recorded.
    private DateTime? selectedMonth;

    public HistoryPage(IPageHost host)
        : base(host)
    {
    }

    public override string Title => "Consumption history";

    public override string Subtitle => "Recorded energy · costs using the selected plan.";

    public override int ContentHeight => ResponsiveLayout.BelowChart + DataTable.HeightFor(DaysOfMonth().Count) + 84;

    private DateTime Month =>
        selectedMonth ?? (MonthUsage.Span(Store.DailyTotals)?.Last ?? new DateTime(FinnishTime.Today.Year, FinnishTime.Today.Month, 1));

    public override void Refresh()
    {
        if (chart is null || previousMonth is null || nextMonth is null)
            return;

        chart.Month = MonthUsage.Create(Month, Store.DailyTotals, Store.Settings);

        var span = MonthUsage.Span(Store.DailyTotals);
        previousMonth.Enabled = span is { } available && Month > available.First;
        nextMonth.Enabled = span is { } known && Month < known.Last;
    }

    protected override void CreateControls()
    {
        AddButton("Export CSV", ResponsiveLayout.ActionButton(0, 160), Export);
        (previousMonth, nextMonth) = AddStepButtons("Previous month", "Next month", () => MoveMonth(-1), () => MoveMonth(1));

        chart = new UsageChart { Bounds = Layout.ChartCard };
        AddControl(chart);
    }

    protected override void PaintBody(Graphics g)
    {
        var settings = Store.Settings;
        var rows = DaysOfMonth().Select(day => ToRow(day.Key, day.Value, settings)).ToList();

        var bottom = PaintTable(g, ResponsiveLayout.BelowChart, rows, "DATE / RECORDED ENERGY", "COST");
        var note = $"{Localization.Translate(TariffCalculator.Name(settings.PriceMode))} · {Localization.Translate("Change the pricing mode in Electricity plan.")}";
        g.DrawLabel(note, new Rectangle(ResponsiveLayout.Margin, bottom + 12, Layout.Width, 60), new TextStyle(10, Accent, Wrap: true));
    }

    private void MoveMonth(int direction)
    {
        selectedMonth = Month.AddMonths(direction);
        Refresh();
        Host.Viewport.SetExtent(ContentHeight);
    }

    // The days of the selected month that have recorded energy, latest first.
    private List<KeyValuePair<DateTime, UsageTotals>> DaysOfMonth() => Store.DailyTotals
        .Where(day => day.Key.Year == Month.Year && day.Key.Month == Month.Month)
        .OrderByDescending(day => day.Key)
        .ToList();

    private static TableRow ToRow(DateTime date, UsageTotals totals, ApplicationSettings settings)
    {
        var missing = totals.MissingKwh(settings);
        return new TableRow(
            date.ToString("d"),
            DisplayFormat.Euros(totals.Cost(settings), missing > 0, 4),
            $"{totals.Kwh:F4} kWh",
            missing > 0 ? Localization.Format("Price missing for {0} kWh", $"{missing:F4}") : "Priced recorded consumption");
    }

    private void Export() => Attempt(() =>
    {
        var path = Path.Combine(Store.Folder, $"usage-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        Store.Export(path);
        ShellLauncher.RevealFile(path);
        Host.Notify("CSV saved and selected in the data folder.");
    }, failureFormat: "Export failed: {0}");
}
