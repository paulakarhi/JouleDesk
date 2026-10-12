namespace JouleDesk;

/// <summary>
/// One sample of the hardware sensors.
/// <paramref name="CpuEnergyWh"/> is the cumulative RAPL counter, so consumption is the difference between two samples.
/// <paramref name="OtherWatts"/> is the sum of the component estimates, when they were collected.
/// </summary>
internal sealed record SensorReading(
    DateTimeOffset At,
    double? CpuEnergyWh,
    double? GpuWatts,
    string CpuStatus,
    string GpuStatus,
    IReadOnlyList<ComponentEstimate>? Components = null,
    double? OtherWatts = null);
