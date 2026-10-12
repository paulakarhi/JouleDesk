namespace JouleDesk;

/// <summary>Windows physical-disk performance counters: busy-time percentages and transfer rates in bytes per second.</summary>
internal sealed record DiskActivity(double IdlePercent, double ReadTime, double WriteTime, double ReadBytes, double WriteBytes);
