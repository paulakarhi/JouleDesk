namespace JouleDesk;

/// <summary>Plausible spot prices. A value outside these limits means a corrupt or hostile response, not a market price.</summary>
internal static class PriceLimits
{
    /// <summary>Cents per kWh with VAT. Prices go below zero; the highest ever cleared are a few hundred cents.</summary>
    public static readonly ValueRange Cents = new(-1000, 10_000);

    public static readonly TimeSpan LongestInterval = TimeSpan.FromDays(1);

    /// <summary>Most prices one download may hold. The service sends about two days of quarter hours.</summary>
    public const int MaxPerDownload = 5_000;

    /// <summary>Most prices the cache may hold: more than a year of quarter hours.</summary>
    public const int MaxCached = 50_000;

    /// <summary>How long prices are kept, counted back from the newest one.</summary>
    public static readonly TimeSpan CacheSpan = TimeSpan.FromDays(400);
}
