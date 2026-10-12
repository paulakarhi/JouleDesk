using System.Management;

namespace JouleDesk;

internal static class WmiQuery
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

    /// <summary>Runs a query against root\cimv2 and projects every row, disposing the WMI objects as it goes.</summary>
    public static List<T> Read<T>(string query, Func<ManagementObject, T> select)
    {
        using var searcher = new ManagementObjectSearcher("root\\cimv2", query,
            new System.Management.EnumerationOptions { Timeout = Timeout });
        using var rows = searcher.Get();

        var result = new List<T>();
        foreach (ManagementObject row in rows)
        {
            using (row)
                result.Add(select(row));
        }
        return result;
    }
}
