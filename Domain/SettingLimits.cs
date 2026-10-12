namespace JouleDesk;

/// <summary>Accepted input ranges for <see cref="ApplicationSettings"/>.</summary>
internal static class SettingLimits
{
    public static readonly ValueRange OtherWatts = new(0, 2000);
    public static readonly ValueRange Efficiency = new(50, 100);
    public static readonly ValueRange BoardAndCoolingWatts = new(0, 1000);
    public static readonly ValueRange RamWattsPerModule = new(0, 100);
    public static readonly ValueRange FixedPrice = new(0, 1000);
    public static readonly ValueRange Margin = new(-100, 100);

    /// <summary>Distribution charge and electricity tax.</summary>
    public static readonly ValueRange Charge = new(0, 100);
}
