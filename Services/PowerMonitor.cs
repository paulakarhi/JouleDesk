namespace JouleDesk;

internal delegate Task<IReadOnlyList<SpotPrice>> PriceSource(CancellationToken cancellationToken);

/// <summary>One point of the live power chart.</summary>
internal sealed record TrendPoint(DateTimeOffset At, double Watts);

/// <summary>
/// Turns periodic sensor samples into usage records and keeps the live figures the views show.
/// Call it from the UI thread: sensors are read on the thread pool, but results are applied where the call was made.
/// </summary>
internal sealed class PowerMonitor : IDisposable
{
    public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan TrendWindow = TimeSpan.FromMinutes(10);

    private readonly UsageStore store;
    private readonly ComponentCatalog catalog;
    private readonly SensorSource readSensors;
    private readonly PriceSource? fetchPrices;
    private readonly CancellationTokenSource shutdown = new();
    private readonly List<TrendPoint> trend = new();

    private SensorReading? previous;
    private bool stopped;
    private bool sampling;
    private bool fetchingPrices;
    private DateTimeOffset nextPriceFetch = DateTimeOffset.MinValue;

    // Bumped whenever earlier samples stop being comparable (new settings, pause); a sample read before
    // the change is dropped when it returns.
    private int generation;

    // A null price source disables spot price updates.
    public PowerMonitor(UsageStore store, ComponentCatalog catalog, SensorSource readSensors, PriceSource? fetchPrices)
    {
        this.store = store;
        this.catalog = catalog;
        this.readSensors = readSensors;
        this.fetchPrices = fetchPrices;
    }

    /// <summary>Raised after any change to the figures below.</summary>
    public event Action? Changed;

    public bool Paused { get; private set; }

    public SensorReading? Latest { get; private set; }

    public double? CpuWatts { get; private set; }

    public double? GpuWatts { get; private set; }

    /// <summary>Estimated draw of the whole PC at the wall.</summary>
    public double? WallWatts { get; private set; }

    public IReadOnlyList<TrendPoint> Trend => trend;

    public string Status { get; private set; } = Localization.Translate("Ready to start");

    public string PriceStatus { get; private set; } = Localization.Translate("Prices awaiting refresh");

    public bool IsStopped => stopped;

    /// <summary>Takes one sample, records the interval since the previous one and refreshes prices when they are due.</summary>
    public async Task SampleAsync()
    {
        if (sampling || stopped)
            return;
        sampling = true;
        var sampleGeneration = generation;
        var settings = store.Settings;
        try
        {
            _ = RefreshPricesAsync();
            if (Paused)
                return;

            var sample = await readSensors(settings, catalog, shutdown.Token);
            if (stopped || Paused || sampleGeneration != generation)
                return;
            Apply(sample, settings);
        }
        catch (OperationCanceledException) when (stopped)
        {
        }
        catch (Exception exception)
        {
            Reset();
            Status = Localization.Format("Monitoring error: {0}", exception.Message);
            Changed?.Invoke();
        }
        finally
        {
            sampling = false;
        }
    }

    public async Task RefreshPricesAsync(bool force = false)
    {
        if (fetchPrices is null || fetchingPrices || stopped || (!force && DateTimeOffset.UtcNow < nextPriceFetch))
            return;
        fetchingPrices = true;
        try
        {
            var prices = await fetchPrices(shutdown.Token);
            if (stopped)
                return;
            store.MergePrices(prices);
            nextPriceFetch = PriceSchedule.NextFetch(DateTimeOffset.UtcNow, store.Prices);
            PriceStatus = Localization.Format("Spot prices updated {0}", FinnishTime.Now.ToString(DisplayFormat.Time));
        }
        catch (OperationCanceledException) when (stopped)
        {
            return;
        }
        catch (Exception)
        {
            nextPriceFetch = DateTimeOffset.UtcNow + PriceSchedule.RetryInterval;
            PriceStatus = Localization.Translate("Price fetch failed · using cache");
        }
        finally
        {
            fetchingPrices = false;
        }
        Changed?.Invoke();
    }

    public async Task UpdateCatalogAsync(string url)
    {
        await catalog.UpdateAsync(url, store.Folder, shutdown.Token);
        Reset();
    }

    public void SetPaused(bool paused)
    {
        Paused = paused;
        Reset();
        Status = Localization.Translate(paused ? "Monitoring paused" : "Waiting for new samples");
        Changed?.Invoke();
    }

    /// <summary>Forgets the previous sample. Call after anything that changes how consecutive samples relate, such as new settings.</summary>
    public void Reset()
    {
        generation++;
        previous = null;
        CpuWatts = null;
        WallWatts = null;
    }

    public void Stop()
    {
        if (stopped)
            return;
        stopped = true;
        shutdown.Cancel();
    }

    public void Dispose()
    {
        Stop();
        shutdown.Dispose();
    }

    private void Apply(SensorReading sample, ApplicationSettings settings)
    {
        Latest = sample;
        GpuWatts = sample.GpuWatts;

        var measurement = MeasurementCalculator.Calculate(previous, sample, settings, store.Prices);
        CpuWatts = measurement?.CpuWatts;
        WallWatts = measurement?.WallWatts;
        if (measurement is null)
        {
            Status = Localization.Translate("Waiting for CPU/GPU samples · see Components");
        }
        else
        {
            store.Append(measurement.Usage);
            trend.Add(new TrendPoint(sample.At, measurement.WallWatts));
            Status = Localization.Format("Monitoring active · updated {0}", FinnishTime.ToLocal(sample.At).ToString(DisplayFormat.TimeWithSeconds));
        }

        trend.RemoveAll(point => point.At < sample.At - TrendWindow);
        previous = sample;
        Changed?.Invoke();
    }
}
