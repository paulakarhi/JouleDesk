namespace JouleDesk;

/// <summary>
/// Spot prices: a bar chart of one day at a time, with day navigation, and below it the list of
/// current and upcoming price periods under the selected plan.
/// </summary>
internal sealed class PricesPage : PageView
{
    private static readonly TimeSpan ListLookBack = TimeSpan.FromHours(1);

    private PriceChart? chart;
    private ThemedButton? previousDay;
    private ThemedButton? nextDay;

    // Null follows the current day, so the chart moves on at midnight until the user picks a day.
    private DateTime? selectedDay;

    public PricesPage(IPageHost host)
        : base(host)
    {
    }

    public override string Title => "Electricity prices";

    public override string Subtitle =>
        $"{Localization.Translate("Finnish price periods · VAT included")} · {Monitor.PriceStatus}";

    public override int ContentHeight => ResponsiveLayout.BelowChart + DataTable.HeightFor(UpcomingPrices().Count) + 24;

    private DateTime Day => selectedDay ?? FinnishTime.Today;

    public override void Refresh()
    {
        if (chart is null || previousDay is null || nextDay is null)
            return;

        var settings = Store.Settings;
        chart.Title = settings.PriceMode == PriceMode.SpotWithFees ? "SPOT + CONTRACT CHARGES" : "SPOT PRICE";
        chart.Day = DayPrices.Create(Day, Store.Prices, settings);

        var span = DayPrices.Span(Store.Prices);
        previousDay.Enabled = span is { } available && Day > available.First;
        nextDay.Enabled = span is { } known && Day < known.Last;
    }

    protected override void CreateControls()
    {
        AddAsyncButton("Refresh prices", ResponsiveLayout.ActionButton(0, 180), () => Monitor.RefreshPricesAsync(force: true));
        (previousDay, nextDay) = AddStepButtons("Previous day", "Next day", () => MoveDay(-1), () => MoveDay(1));

        chart = new PriceChart { Bounds = Layout.ChartCard };
        AddControl(chart);
    }

    protected override void PaintBody(Graphics g) =>
        PaintTable(g, ResponsiveLayout.BelowChart, UpcomingPrices().Select(ToRow).ToList(), "PERIOD / SPOT PRICE", "SELECTED PRICE");

    private void MoveDay(int direction)
    {
        selectedDay = Day.AddDays(direction);
        Refresh();
    }

    // The period in progress and everything after it that is known.
    private List<SpotPrice> UpcomingPrices()
    {
        var from = DateTimeOffset.UtcNow - ListLookBack;
        return Store.Prices.Where(price => price.End > from).OrderBy(price => price.Start).ToList();
    }

    private TableRow ToRow(SpotPrice price)
    {
        var settings = Store.Settings;
        var period = $"{FinnishTime.ToLocal(price.Start).ToString(DisplayFormat.DayAndTime)} – {FinnishTime.ToLocal(price.End).ToString(DisplayFormat.Time)}";
        return new TableRow(
            period,
            $"{TariffCalculator.Cents(price.PriceCents, settings):F3} {Localization.Translate("ct/kWh")}",
            Localization.Format("Spot {0} ct/kWh", $"{price.PriceCents:F3}"),
            TariffCalculator.Name(settings.PriceMode));
    }
}
