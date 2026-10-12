namespace JouleDesk;

/// <summary>Plausible ranges for sensor readings. A reading outside its range is treated as a sensor error, not a measurement.</summary>
internal static class SensorLimits
{
    public static readonly ValueRange CpuWatts = new(0, 1000);
    public static readonly ValueRange GpuWatts = new(0, 10_000);
    public static readonly ValueRange OtherWatts = new(0, 10_000);
}
