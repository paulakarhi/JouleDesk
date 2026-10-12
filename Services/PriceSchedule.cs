namespace JouleDesk;

/// <summary>
/// When to ask the price service again. It publishes the next day's prices in the early afternoon, Finnish time,
/// and asks to be polled lightly: twice a day is enough. Between the usual time of publication and the arrival of
/// tomorrow's prices it is asked more often.
/// </summary>
internal static class PriceSchedule
{
    /// <summary>How long to wait after a request that failed, or while tomorrow's prices are still missing.</summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(12);
    private const int PublicationHour = 14;

    /// <summary>
    /// The next moment to fetch, after a successful fetch at <paramref name="now"/>
    /// that left <paramref name="prices"/> in the cache.
    /// </summary>
    public static DateTimeOffset NextFetch(DateTimeOffset now, IReadOnlyList<SpotPrice> prices)
    {
        var today = FinnishTime.ToLocal(now).Date;
        var publication = FinnishTime.ToInstant(today.AddHours(PublicationHour));
        if (now < publication)
            return Earlier(now + RefreshInterval, publication);

        var tomorrowKnown = prices.Count > 0 && prices[^1].End >= FinnishTime.ToInstant(today.AddDays(2));
        return tomorrowKnown
            ? Earlier(now + RefreshInterval, FinnishTime.ToInstant(today.AddDays(1).AddHours(PublicationHour)))
            : now + RetryInterval;
    }

    private static DateTimeOffset Earlier(DateTimeOffset first, DateTimeOffset second) => first < second ? first : second;
}
