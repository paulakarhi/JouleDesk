using System.Diagnostics;
using System.Globalization;

namespace JouleDesk;

/// <summary>Reads GPU power from the NVIDIA driver's <c>nvidia-smi</c> command line tool.</summary>
internal static class GpuTelemetry
{
    private const string Command = "nvidia-smi.exe";
    private const string Arguments = "--query-gpu=name,power.draw --format=csv,noheader,nounits";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Parses one "name, watts" line per adapter and sums the power of all of them.
    /// Any adapter without a usable reading fails the whole parse, so a partial total is never reported.
    /// </summary>
    public static (double Watts, string Name) Parse(string output)
    {
        var names = new List<string>();
        var totalWatts = 0.0;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Adapter names can contain commas, so the power is the part after the last one.
            var separator = line.LastIndexOf(',');
            if (separator < 1 ||
                !double.TryParse(line[(separator + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var watts) ||
                !SensorLimits.GpuWatts.Contains(watts))
                throw new InvalidDataException(Localization.Translate("GPU power reading is unavailable."));

            totalWatts += watts;
            names.Add(line[..separator].Trim());
        }
        if (names.Count == 0)
            throw new InvalidDataException(Localization.Translate("GPU power reading is unavailable."));
        return (totalWatts, string.Join(" + ", names));
    }

    public static async Task<(double Watts, string Name)> ReadAsync(CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(Command, Arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();

        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var text = await output;
            await error;
            if (process.ExitCode != 0)
                throw new InvalidDataException(Localization.Translate("GPU command failed."));
            return Parse(text);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException(Localization.Translate("GPU sensor timed out."));
        }
    }
}
