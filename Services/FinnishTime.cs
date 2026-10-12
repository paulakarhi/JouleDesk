namespace JouleDesk;

/// <summary>
/// Spot prices, daily totals and the price chart all follow Finnish time,
/// whatever time zone or interface language the PC uses.
/// </summary>
internal static class FinnishTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");

    public static DateTime Now => ToLocal(DateTimeOffset.UtcNow);

    public static DateTime Today => Now.Date;

    public static DateTime ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone).DateTime;

    /// <summary>
    /// The instant a Finnish wall-clock time occurs. A time skipped by the spring clock change
    /// maps to the same moment as the hour after it.
    /// </summary>
    public static DateTimeOffset ToInstant(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(unspecified))
            unspecified = unspecified.AddHours(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, Zone));
    }
}
