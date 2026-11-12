namespace JouleDesk;

/// <summary>
/// Cutting usage records at the multiples of a bucket length, such as a minute, counted from midnight UTC.
/// Finnish time differs from UTC by whole hours, so the buckets line up with the local clock too.
/// </summary>
internal static class UsageBuckets
{
    public static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan QuarterHour = TimeSpan.FromMinutes(15);

    /// <summary>The number of the bucket that holds an instant.</summary>
    public static long Of(DateTimeOffset instant, TimeSpan length) => instant.UtcTicks / length.Ticks;

    /// <summary>The instant the bucket that holds <paramref name="instant"/> ends.</summary>
    public static DateTimeOffset EndOf(DateTimeOffset instant, TimeSpan length) =>
        new((Of(instant, length) + 1) * length.Ticks, TimeSpan.Zero);

    /// <summary>
    /// Cuts a record wherever its time crosses a bucket boundary. Energy and cost follow the share of time,
    /// so the pieces add up to the record.
    /// </summary>
    public static IEnumerable<UsageRecord> Cut(UsageRecord row, TimeSpan length)
    {
        var totalTicks = (double)(row.End - row.Start).Ticks;
        var from = row.Start;
        while (from < row.End)
        {
            var boundary = EndOf(from, length);
            var to = boundary < row.End ? boundary : row.End;
            var share = (to - from).Ticks / totalTicks;
            yield return row with { Start = from, End = to, Kwh = row.Kwh * share, Euros = row.Euros * share };
            from = to;
        }
    }
}
