using System.ComponentModel;
using System.Globalization;

namespace JouleDesk;

/// <summary>Reads the sensors that are sampled on every monitoring tick.</summary>
internal delegate Task<SensorReading> SensorSource(ApplicationSettings settings, ComponentCatalog catalog, CancellationToken cancellationToken);

internal static class SensorReader
{
    private const string EnergyMeterQuery = "SELECT Name, Energy FROM Win32_PerfFormattedData_PowerMeterCounter_EnergyMeter";
    private const string CpuPackageCounter = "RAPL_Package0_PKG";

    // The Windows energy meter counts in picowatt-hours.
    private const double PicoWattHoursPerWattHour = 1e12;

    private const int CpuReadAttempts = 2;
    private static readonly TimeSpan CpuRetryDelay = TimeSpan.FromMilliseconds(120);

    /// <summary>
    /// Takes one sample: the cumulative CPU energy counter, the GPU power and estimates for the other components.
    /// A sensor that cannot be read leaves its value null and explains why in the status text.
    /// </summary>
    public static Task<SensorReading> ReadAsync(ApplicationSettings? settings = null, ComponentCatalog? catalog = null,
        CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        settings ??= ApplicationSettings.Defaults;

        var (cpuEnergy, cpuStatus) = await ReadCpuEnergyAsync(cancellationToken);

        // The timestamp belongs to the CPU counter; the GPU query below takes much longer.
        var measuredAt = DateTimeOffset.UtcNow;

        var (gpuWatts, gpuStatus) = await ReadGpuAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var components = settings.ComponentEstimates
            ? ComponentEstimator.Read(settings, catalog ?? new ComponentCatalog())
            : new List<ComponentEstimate>
            {
                new(Localization.Translate("Other PC parts"), settings.OtherWatts, EstimateKind.General,
                    Localization.Translate("User's fixed assumption"), "")
            };
        return new SensorReading(measuredAt, cpuEnergy, gpuWatts, cpuStatus, gpuStatus, components, components.Sum(part => part.Watts));
    }, cancellationToken);

    private static async Task<(double? Energy, string Status)> ReadCpuEnergyAsync(CancellationToken cancellationToken)
    {
        var status = Localization.Translate("CPU sensor not found");
        for (var attempt = 1; attempt <= CpuReadAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (TryReadPackageEnergy() is { } energy)
                    return (energy, Localization.Translate("Intel RAPL · CPU package"));
            }
            catch (Exception exception)
            {
                // WMI reports failures through several unrelated exception types.
                status = "CPU: " + exception.Message;
            }

            if (attempt < CpuReadAttempts)
                await Task.Delay(CpuRetryDelay, cancellationToken);
        }
        return (null, status);
    }

    private static double? TryReadPackageEnergy()
    {
        var package = WmiQuery.Read(EnergyMeterQuery, row => (Name: row["Name"] as string, Energy: row["Energy"]))
            .FirstOrDefault(row => row.Name == CpuPackageCounter);
        if (package.Energy is null)
            return null;

        var wattHours = Convert.ToDouble(package.Energy, CultureInfo.InvariantCulture) / PicoWattHoursPerWattHour;
        return double.IsFinite(wattHours) && wattHours >= 0 ? wattHours : null;
    }

    private static async Task<(double? Watts, string Status)> ReadGpuAsync(CancellationToken cancellationToken)
    {
        try
        {
            var (watts, name) = await GpuTelemetry.ReadAsync(cancellationToken);
            return (watts, name);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Win32Exception)
        {
            // nvidia-smi could not be started: no NVIDIA driver is installed.
            return (null, Localization.Translate("NVIDIA sensor not found"));
        }
        catch (Exception exception)
        {
            return (null, "GPU: " + exception.Message);
        }
    }
}
