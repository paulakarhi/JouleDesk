using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;

namespace JouleDesk;

/// <summary>
/// Estimates the power of the parts that have no power sensor: drives from their profile and measured activity,
/// memory and the rest of the machine from adjustable assumptions.
/// </summary>
internal static class ComponentEstimator
{
    private const string DiskInventoryQuery = "SELECT Index, Model FROM Win32_DiskDrive";
    private const string MemoryInventoryQuery = "SELECT PartNumber, Capacity FROM Win32_PhysicalMemory";
    private const string DiskActivityQuery = "SELECT * FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk";
    private const double BytesPerGiB = 1024.0 * 1024 * 1024;

    private sealed record Disk(int Index, string Model);

    private sealed record Memory(string Model, double GiB);

    private sealed record Inventory(IReadOnlyList<Disk> Disks, IReadOnlyList<Memory> Memory);

    // Installed hardware rarely changes, so it is read once. A failed read is not cached and is retried.
    private static readonly Lazy<Inventory> InstalledHardware = new(ReadInventory, LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Power of a drive for the given activity. Idle and busy power are blended by how busy the drive was;
    /// this is a heuristic on busy time, not a measurement. Without usable counters the profile's typical figure is used.
    /// </summary>
    public static double DiskWatts(DiskProfile profile, DiskActivity? activity)
    {
        if (activity is null || !HasFiniteCounters(activity))
            return profile.TypicalWatts;

        var busyShare = Math.Clamp(1 - activity.IdlePercent / 100, 0, 1);

        // Read/write split from busy time, falling back to throughput when the time counters are both zero.
        var reads = Math.Max(0, activity.ReadTime);
        var writes = Math.Max(0, activity.WriteTime);
        if (reads + writes == 0)
        {
            reads = Math.Max(0, activity.ReadBytes);
            writes = Math.Max(0, activity.WriteBytes);
        }
        var writeShare = reads + writes > 0 ? writes / (reads + writes) : 0.5;

        var busyWatts = profile.ReadWatts * (1 - writeShare) + profile.WriteWatts * writeShare;
        return profile.IdleWatts * (1 - busyShare) + busyWatts * busyShare;
    }

    public static List<ComponentEstimate> Read(ApplicationSettings settings, ComponentCatalog catalog)
    {
        try
        {
            var inventory = InstalledHardware.Value;
            var activity = ReadDiskActivity();

            var estimates = new List<ComponentEstimate>();
            estimates.AddRange(inventory.Disks.Select(disk => EstimateDisk(disk, activity, catalog)));
            estimates.AddRange(inventory.Memory.Select(memory => EstimateMemory(memory, settings)));
            estimates.Add(new ComponentEstimate(
                Localization.Translate("Motherboard, cooling and other parts"), settings.BoardAndCoolingWatts, EstimateKind.General,
                Localization.Translate("Adjustable total · excludes CPU, GPU, RAM and drives"), ""));
            return estimates;
        }
        catch (Exception exception)
        {
            // Inventory failures come from WMI in many forms; one flat estimate keeps the measurement going.
            return new List<ComponentEstimate>
            {
                new(Localization.Translate("Other PC parts"), settings.OtherWatts, EstimateKind.GeneralFallback,
                    Localization.Format("Component detection failed: {0}", exception.Message), "")
            };
        }
    }

    private static bool HasFiniteCounters(DiskActivity activity) =>
        new[] { activity.IdlePercent, activity.ReadTime, activity.WriteTime, activity.ReadBytes, activity.WriteBytes }.All(double.IsFinite);

    private static ComponentEstimate EstimateDisk(Disk disk, IReadOnlyDictionary<int, DiskActivity> activity, ComponentCatalog catalog)
    {
        var known = catalog.Find(disk.Model);
        var profile = known ?? DiskProfile.Generic(disk.Model);
        activity.TryGetValue(disk.Index, out var busy);
        return new ComponentEstimate(disk.Model, DiskWatts(profile, busy),
            known is null ? EstimateKind.General : EstimateKind.ModelBased, DescribeActivity(busy), profile.Source);
    }

    private static string DescribeActivity(DiskActivity? activity) => activity is null
        ? Localization.Translate("Activity unavailable · typical assumption")
        : Localization.Format("Active {0}% · read {1} / write {2} MB/s",
            $"{Math.Clamp(100 - activity.IdlePercent, 0, 100):F0}", $"{activity.ReadBytes / 1e6:F2}", $"{activity.WriteBytes / 1e6:F2}");

    private static ComponentEstimate EstimateMemory(Memory memory, ApplicationSettings settings) => new(
        $"{memory.Model} · {memory.GiB:F0} GB", settings.RamWattsPerModule, EstimateKind.General,
        Localization.Translate("Adjustable W/module · memory fill is not a power meter"), "");

    private static Inventory ReadInventory()
    {
        var disks = WmiQuery.Read(DiskInventoryQuery, row => new Disk(
            Convert.ToInt32(row["Index"], CultureInfo.InvariantCulture),
            ((string?)row["Model"] ?? Localization.Translate("Unknown drive")).Trim()));
        var memory = WmiQuery.Read(MemoryInventoryQuery, row => new Memory(
            ((string?)row["PartNumber"] ?? "RAM").Trim(),
            Convert.ToDouble(row["Capacity"], CultureInfo.InvariantCulture) / BytesPerGiB));
        return new Inventory(disks, memory);
    }

    // Activity is optional: without the counters the drives are estimated from their typical power.
    private static Dictionary<int, DiskActivity> ReadDiskActivity()
    {
        var activity = new Dictionary<int, DiskActivity>();
        try
        {
            foreach (var (name, counters) in WmiQuery.Read(DiskActivityQuery, ReadActivityRow))
            {
                // Instances are named "<disk index> <drive letters>", plus a "_Total" row that is skipped here.
                if (int.TryParse(name.Split(' ')[0], out var index))
                    activity[index] = counters;
            }
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException or COMException)
        {
        }
        return activity;
    }

    private static (string Name, DiskActivity Counters) ReadActivityRow(ManagementObject row)
    {
        double Counter(string name) => Convert.ToDouble(row[name] ?? 0, CultureInfo.InvariantCulture);
        return ((string?)row["Name"] ?? "", new DiskActivity(
            Counter("PercentIdleTime"), Counter("PercentDiskReadTime"), Counter("PercentDiskWriteTime"),
            Counter("DiskReadBytesPersec"), Counter("DiskWriteBytesPersec")));
    }
}
