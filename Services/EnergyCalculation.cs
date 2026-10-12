namespace JouleDesk;

internal static class EnergyCalculation
{
    /// <summary>Estimated draw at the wall: the DC load divided by the power supply efficiency.</summary>
    public static double WallWatts(double cpuWatts, double gpuWatts, ApplicationSettings settings, double? otherWatts = null) =>
        (cpuWatts + gpuWatts + (otherWatts ?? settings.OtherWatts)) / (settings.Efficiency / 100);

    /// <summary>
    /// Turns a constant load over an interval into usage records. The interval is cut at every price
    /// boundary and at Finnish midnight, so each record has one price and belongs to one day.
    /// Records stay unpriced when no spot price covers them.
    /// </summary>
    public static List<UsageRecord> Split(DateTimeOffset start, DateTimeOffset end, double watts, double extraCents, IReadOnlyList<SpotPrice> prices)
    {
        var records = new List<UsageRecord>();
        if (end <= start || watts < 0 || !double.IsFinite(watts) || !double.IsFinite(extraCents))
            return records;

        var boundaries = new SortedSet<DateTimeOffset> { start, end };
        foreach (var price in SpotPrices.Between(prices, start, end))
        {
            if (price.Start > start)
                boundaries.Add(price.Start);
            if (price.End < end)
                boundaries.Add(price.End);
        }

        for (var day = FinnishTime.ToLocal(start).Date.AddDays(1); ; day = day.AddDays(1))
        {
            var midnight = FinnishTime.ToInstant(day);
            if (midnight >= end)
                break;
            if (midnight > start)
                boundaries.Add(midnight);
        }

        var points = boundaries.ToArray();
        for (var index = 0; index < points.Length - 1; index++)
        {
            var (from, to) = (points[index], points[index + 1]);
            var kwh = watts * (to - from).TotalHours / 1000;
            var price = SpotPrices.At(prices, from);
            var euros = price is null ? (double?)null : kwh * (price.PriceCents + extraCents) / 100;
            records.Add(new UsageRecord(from, to, kwh, euros, extraCents));
        }
        return records;
    }
}
