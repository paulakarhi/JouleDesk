namespace JouleDesk;

/// <summary>One bar of the price chart: the price in effect from <paramref name="Start"/> up to <paramref name="End"/>.</summary>
internal sealed record PriceBar(DateTimeOffset Start, DateTimeOffset End, double Cents);

/// <summary>The prices of one Finnish calendar day, with the summary figures shown above the price chart.</summary>
internal sealed class DayPrices
{
    private DayPrices(DateTime date, IReadOnlyList<PriceBar> bars)
    {
        Date = date;
        Start = FinnishTime.ToInstant(date);
        End = FinnishTime.ToInstant(date.AddDays(1));
        Bars = bars;

        var covered = bars.Sum(bar => (bar.End - bar.Start).TotalHours);
        if (covered > 0)
            Average = bars.Sum(bar => bar.Cents * (bar.End - bar.Start).TotalHours) / covered;
        Lowest = bars.MinBy(bar => bar.Cents);
        Highest = bars.MaxBy(bar => bar.Cents);
    }

    public DateTime Date { get; }

    /// <summary>The instant the day begins. The day is 23 or 25 hours long when the clocks change.</summary>
    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public IReadOnlyList<PriceBar> Bars { get; }

    /// <summary>Average price weighted by interval length, or null when the day has no prices.</summary>
    public double? Average { get; }

    public PriceBar? Lowest { get; }

    public PriceBar? Highest { get; }

    public static DayPrices Create(DateTime date, IReadOnlyList<SpotPrice> prices, ApplicationSettings settings)
    {
        var day = date.Date;
        var start = FinnishTime.ToInstant(day);
        var end = FinnishTime.ToInstant(day.AddDays(1));
        var bars = SpotPrices.Between(prices, start, end)
            .Select(price => new PriceBar(
                price.Start > start ? price.Start : start,
                price.End < end ? price.End : end,
                TariffCalculator.MarketCents(price.PriceCents, settings)))
            .ToList();
        return new DayPrices(day, bars);
    }

    /// <summary>The first and last day that have prices, or null for an empty list.</summary>
    public static (DateTime First, DateTime Last)? Span(IReadOnlyList<SpotPrice> prices)
    {
        if (prices.Count == 0)
            return null;
        return (FinnishTime.ToLocal(prices[0].Start).Date, FinnishTime.ToLocal(prices[^1].End.AddTicks(-1)).Date);
    }
}
