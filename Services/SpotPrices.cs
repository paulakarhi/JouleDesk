namespace JouleDesk;

/// <summary>Validation and lookup for spot price lists, which are kept ordered by start time without overlaps.</summary>
internal static class SpotPrices
{
    public static IReadOnlyList<SpotPrice> Validate(IEnumerable<SpotPrice> prices)
    {
        var ordered = prices.OrderBy(price => price?.Start).ToList();
        if (ordered.Count == 0)
            throw new InvalidDataException(Localization.Translate("The API returned no prices."));
        if (ordered.Count > PriceLimits.MaxCached)
            throw new InvalidDataException(Localization.Translate("The API returned too many prices."));

        for (var index = 0; index < ordered.Count; index++)
        {
            var price = ordered[index];
            if (price is null || price.End <= price.Start || price.End - price.Start > PriceLimits.LongestInterval ||
                !PriceLimits.Cents.Contains(price.PriceCents))
                throw new InvalidDataException(Localization.Translate("Invalid price interval."));
            if (index > 0 && ordered[index - 1].End > price.Start)
                throw new InvalidDataException(Localization.Translate("Overlapping price intervals."));
        }
        return ordered;
    }

    /// <summary>The prices of the last <see cref="PriceLimits.CacheSpan"/>, counted back from the newest one.</summary>
    public static IReadOnlyList<SpotPrice> Recent(IReadOnlyList<SpotPrice> prices)
    {
        if (prices.Count == 0)
            return prices;

        var cutoff = prices[^1].End - PriceLimits.CacheSpan;
        return prices.Where(price => price.End > cutoff).ToList();
    }

    /// <summary>The price in effect at an instant, or null when the list has none.</summary>
    public static SpotPrice? At(IReadOnlyList<SpotPrice> prices, DateTimeOffset instant)
    {
        var index = FirstEndingAfter(prices, instant);
        return index < prices.Count && prices[index].Start <= instant ? prices[index] : null;
    }

    /// <summary>The prices that overlap the span from <paramref name="start"/> up to <paramref name="end"/>.</summary>
    public static IEnumerable<SpotPrice> Between(IReadOnlyList<SpotPrice> prices, DateTimeOffset start, DateTimeOffset end)
    {
        for (var index = FirstEndingAfter(prices, start); index < prices.Count && prices[index].Start < end; index++)
            yield return prices[index];
    }

    // Binary search; valid because intervals do not overlap, so their end times are ordered too.
    private static int FirstEndingAfter(IReadOnlyList<SpotPrice> prices, DateTimeOffset instant)
    {
        int low = 0, high = prices.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (prices[middle].End > instant)
                high = middle;
            else
                low = middle + 1;
        }
        return low;
    }
}
