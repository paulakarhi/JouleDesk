namespace JouleDesk;

/// <summary>One day of recorded energy and what it costs under the selected plan.</summary>
internal sealed record DayUsage(DateTime Date, double Kwh, double Euros, double MissingKwh);

/// <summary>The recorded energy of one calendar month, day by day, with the figures shown above the usage chart.</summary>
internal sealed class MonthUsage
{
    private MonthUsage(DateTime month, IReadOnlyList<DayUsage> days)
    {
        Month = month;
        Days = days;
        Kwh = days.Sum(day => day.Kwh);
        Euros = days.Sum(day => day.Euros);
        MissingKwh = days.Sum(day => day.MissingKwh);
        Highest = days.Where(day => day.Kwh > 0).MaxBy(day => day.Kwh);
        Recorded = days.SkipWhile(day => day.Kwh == 0).Reverse().SkipWhile(day => day.Kwh == 0).Reverse().ToList();
    }

    /// <summary>The first day of the month.</summary>
    public DateTime Month { get; }

    /// <summary>Every day of the month, including days without recorded energy.</summary>
    public IReadOnlyList<DayUsage> Days { get; }

    /// <summary>
    /// The days from the first to the last one with recorded energy, in date order. Days in between without any
    /// are included, since nothing was recorded on them. Empty when nothing was recorded in the month.
    /// </summary>
    public IReadOnlyList<DayUsage> Recorded { get; }

    public double Kwh { get; }

    public double Euros { get; }

    /// <summary>Energy that cannot be priced yet, which the cost leaves out.</summary>
    public double MissingKwh { get; }

    /// <summary>The day with the most energy, or null when nothing was recorded.</summary>
    public DayUsage? Highest { get; }

    public static MonthUsage Create(DateTime month, IReadOnlyDictionary<DateTime, UsageTotals> totals, ApplicationSettings settings)
    {
        var first = new DateTime(month.Year, month.Month, 1);
        var days = Enumerable.Range(0, DateTime.DaysInMonth(first.Year, first.Month))
            .Select(offset => first.AddDays(offset))
            .Select(date => totals.TryGetValue(date, out var sum)
                ? new DayUsage(date, sum.Kwh, sum.Cost(settings), sum.MissingKwh(settings))
                : new DayUsage(date, 0, 0, 0))
            .ToList();
        return new MonthUsage(first, days);
    }

    /// <summary>The first and last month that have recorded energy, or null when there is none.</summary>
    public static (DateTime First, DateTime Last)? Span(IReadOnlyDictionary<DateTime, UsageTotals> totals)
    {
        if (totals.Count == 0)
            return null;

        static DateTime MonthOf(DateTime date) => new(date.Year, date.Month, 1);
        return (MonthOf(totals.Keys.Min()), MonthOf(totals.Keys.Max()));
    }
}
