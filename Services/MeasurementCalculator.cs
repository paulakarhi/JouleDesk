namespace JouleDesk;

internal sealed record MeasurementResult(double CpuWatts, double WallWatts, IReadOnlyList<UsageRecord> Usage);

internal static class MeasurementCalculator
{
    /// <summary>
    /// Longest gap between two samples that still counts as continuous monitoring.
    /// Longer gaps (sleep, a stalled sensor, clock jumps) are left unrecorded rather than guessed.
    /// </summary>
    public static readonly TimeSpan MaximumGap = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Derives the draw between two consecutive samples. Returns null when either sample lacks a sensor value
    /// or the pair cannot describe a continuous, plausible interval.
    /// </summary>
    public static MeasurementResult? Calculate(SensorReading? previous, SensorReading current,
        ApplicationSettings settings, IReadOnlyList<SpotPrice> prices)
    {
        if (previous?.CpuEnergyWh is not double startEnergy || current.CpuEnergyWh is not double endEnergy ||
            previous.GpuWatts is not double startGpu || current.GpuWatts is not double endGpu)
            return null;

        var elapsed = current.At - previous.At;
        if (elapsed <= TimeSpan.Zero || elapsed > MaximumGap || endEnergy < startEnergy)
            return null;

        var cpuWatts = (endEnergy - startEnergy) * 3600 / elapsed.TotalSeconds;
        var startOther = previous.OtherWatts ?? settings.OtherWatts;
        var endOther = current.OtherWatts ?? settings.OtherWatts;
        if (!SensorLimits.CpuWatts.Contains(cpuWatts) || !SensorLimits.GpuWatts.Contains(startGpu) || !SensorLimits.GpuWatts.Contains(endGpu) ||
            !SensorLimits.OtherWatts.Contains(startOther) || !SensorLimits.OtherWatts.Contains(endOther))
            return null;

        // The CPU figure is already an average over the interval; GPU and component readings are
        // instantaneous, so their two ends are averaged for the energy total.
        var wallWatts = EnergyCalculation.WallWatts(cpuWatts, endGpu, settings, endOther);
        var averageWatts = EnergyCalculation.WallWatts(cpuWatts, (startGpu + endGpu) / 2, settings, (startOther + endOther) / 2);
        var usage = EnergyCalculation.Split(previous.At, current.At, averageWatts, settings.Extras, prices);
        return new MeasurementResult(cpuWatts, wallWatts, usage);
    }
}
