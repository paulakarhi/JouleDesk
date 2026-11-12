namespace JouleDesk;

/// <summary>
/// Consumption minute by minute: a chart of one day, with day navigation, and below it a chart that magnifies one hour
/// of that day so that every minute has its own bar.
/// </summary>
internal sealed class MinutesPage : PageView
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private ConsumptionChart? dayChart;
    private ConsumptionChart? hourChart;
    private ThemedButton? previousDay;
    private ThemedButton? nextDay;
    private ThemedButton? earlierHour;
    private ThemedButton? laterHour;
    private DayConsumption? consumption;

    // Null follows the current day, so the chart moves on at midnight until the user picks a day.
    private DateTime? selectedDay;

    // Null follows the latest hour with recorded usage, so the magnified hour stays current while monitoring runs.
    private int? selectedHour;

    /// <param name="host">The window that shows the page.</param>
    /// <param name="day">The day to show first; null shows the current day.</param>
    public MinutesPage(IPageHost host, DateTime? day = null)
        : base(host)
    {
        selectedDay = day?.Date;
    }

    public override string Title => "Consumption minute by minute";

    public override string Subtitle => "Average power of every minute · costs using the selected plan.";

    public override int ContentHeight => ResponsiveLayout.BelowSecondChart + 90;

    private DateTime Day => selectedDay ?? FinnishTime.Today;

    private int ShownHour => consumption is null ? 0 : selectedHour ?? LatestHour(consumption);

    public override void Refresh()
    {
        if (dayChart is null || hourChart is null || previousDay is null || nextDay is null || earlierHour is null || laterHour is null)
            return;

        var start = FinnishTime.ToInstant(Day);
        var end = FinnishTime.ToInstant(Day.AddDays(1));
        consumption = DayConsumption.Create(Day, Store.RecordsBetween(start, end), Store.Settings);

        var hour = Math.Clamp(ShownHour, 0, consumption.HourCount - 1);
        var hourStart = consumption.HourStart(hour);
        dayChart.Show(consumption, consumption.Start, consumption.End - consumption.Start, DisplayFormat.DayLabel(Day));
        dayChart.Highlight(hourStart, Hour);
        hourChart.Show(consumption, hourStart, Hour, $"{LocalTime(hourStart)}–{LocalTime(hourStart + Hour)}");

        var span = RecordedDays();
        previousDay.Enabled = span is { } available && Day > available.First;
        nextDay.Enabled = span is { } known && Day < known.Last;
        earlierHour.Enabled = hour > 0;
        laterHour.Enabled = hour < consumption.HourCount - 1;
    }

    protected override void CreateControls()
    {
        (earlierHour, laterHour) = AddStepButtons("Earlier hour", "Later hour", () => MoveHour(-1), () => MoveHour(1),
            atLeft: true, previousGlyph: "«", nextGlyph: "»");
        (previousDay, nextDay) = AddStepButtons("Previous day", "Next day", () => MoveDay(-1), () => MoveDay(1));

        dayChart = new ConsumptionChart("POWER BY MINUTE", "No consumption recorded this day") { Bounds = Layout.ChartCard };
        dayChart.ItemClicked += minute => ChooseHour(minute.Start);
        AddControl(dayChart);

        hourChart = new ConsumptionChart("SELECTED HOUR", "No consumption recorded this hour") { Bounds = Layout.SecondChartCard };
        AddControl(hourChart);
    }

    protected override void PaintBody(Graphics g) =>
        g.DrawLabel("‹ › change the day and « » the hour. Click the day chart to magnify an hour.",
            new Rectangle(ResponsiveLayout.Margin, ResponsiveLayout.BelowSecondChart - 6, Layout.Width, 60), TextStyles.Hint);

    private void MoveDay(int direction)
    {
        selectedDay = Day.AddDays(direction);
        selectedHour = null;
        Refresh();
    }

    private void MoveHour(int direction)
    {
        selectedHour = Math.Clamp(ShownHour + direction, 0, (consumption?.HourCount ?? 1) - 1);
        Refresh();
    }

    private void ChooseHour(DateTimeOffset instant)
    {
        if (consumption is null)
            return;

        selectedHour = consumption.HourOf(instant);
        Refresh();
    }

    // The hour of the newest minute with recorded usage, or the first hour of a day without any.
    private static int LatestHour(DayConsumption day) => day.Minutes.Count == 0 ? 0 : day.HourOf(day.Minutes[^1].Start);

    private static string LocalTime(DateTimeOffset instant) => FinnishTime.ToLocal(instant).ToString(DisplayFormat.Time);

    // The first and last day with recorded usage, or null when nothing has been recorded.
    private (DateTime First, DateTime Last)? RecordedDays()
    {
        var days = Store.DailyTotals;
        return days.Count == 0 ? null : (days.Keys.Min(), days.Keys.Max());
    }
}
