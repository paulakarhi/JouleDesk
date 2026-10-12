using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace JouleDesk;

/// <summary>
/// The user's data folder: settings, cached spot prices and the recorded usage history.
/// Usage is appended as JSON lines; the other files are replaced atomically.
/// </summary>
internal sealed class UsageStore
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private const int TailBytes = 1024;
    private static readonly long QuarterHourTicks = TimeSpan.FromMinutes(15).Ticks;

    private readonly List<UsageRecord> history = new();
    private readonly Dictionary<DateTime, UsageTotals> dailyTotals = new();

    public UsageStore(string? folder = null)
    {
        Folder = folder ?? DataFiles.DefaultFolder;
        Directory.CreateDirectory(Folder);
        Settings = LoadSettings();
        LoadPrices();
        LoadHistory();
        IndexTotals(history);
    }

    public string Folder { get; }

    public ApplicationSettings Settings { get; private set; }

    /// <summary>Cached spot prices, ordered by start time.</summary>
    public IReadOnlyList<SpotPrice> Prices { get; private set; } = Array.Empty<SpotPrice>();

    public IReadOnlyList<UsageRecord> History => history;

    /// <summary>Totals per Finnish calendar day, kept up to date as records are added.</summary>
    public IReadOnlyDictionary<DateTime, UsageTotals> DailyTotals => dailyTotals;

    /// <summary>Description of the first problem met while loading saved data, or null when everything loaded.</summary>
    public string? Warning { get; private set; }

    public UsageTotals TotalsFor(DateTime day) => dailyTotals.GetValueOrDefault(day.Date, new UsageTotals());

    public UsageTotals TotalsForMonth(DateTime day) => dailyTotals
        .Where(pair => pair.Key.Year == day.Year && pair.Key.Month == day.Month)
        .Aggregate(new UsageTotals(), (sum, pair) => sum.Add(pair.Value));

    public void SaveSettings(ApplicationSettings candidate)
    {
        SettingsValidator.Validate(candidate);
        AtomicFile.Write(PathOf(DataFiles.Settings), JsonSerializer.Serialize(candidate, JsonOptions));
        Settings = candidate;
    }

    /// <summary>
    /// Records new intervals. They must be valid and may not overlap anything already recorded.
    /// Neighbouring intervals within the same quarter hour are joined into one record, so a day of five-second samples
    /// takes up 96 records instead of 17 280. The totals and the cost of a joined record equal the sum of its parts.
    /// </summary>
    public void Append(IEnumerable<UsageRecord> rows)
    {
        var items = rows.ToList();
        if (items.Count == 0)
            return;

        var lastEnd = history.Count == 0 ? DateTimeOffset.MinValue : history[^1].End;
        foreach (var row in items)
        {
            if (!IsValid(row) || row.Start < lastEnd)
                throw new InvalidDataException(Localization.Translate("Invalid or overlapping usage interval."));
            lastEnd = row.End;
        }

        bool joinsLast;
        List<UsageRecord> written;
        using (var stream = new FileStream(PathOf(DataFiles.Usage), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
        {
            // The last stored line is replaced by its joined version, but only when it really is the last record.
            var lineStart = 0L;
            joinsLast = history.Count > 0 && CanJoin(history[^1], items[0]) && EndsWithRecord(stream, history[^1], out lineStart);
            written = Join(joinsLast ? history[^1] : null, items);

            if (joinsLast)
            {
                stream.SetLength(lineStart);
            }
            else if (stream.Length > 0)
            {
                // An interrupted write can leave a partial last line; a newline keeps it from swallowing the next record.
                stream.Seek(-1, SeekOrigin.End);
                if (stream.ReadByte() != '\n')
                    stream.WriteByte((byte)'\n');
            }
            stream.Seek(0, SeekOrigin.End);
            using var writer = new StreamWriter(stream);
            foreach (var record in written)
                writer.WriteLine(JsonSerializer.Serialize(record));
        }

        if (joinsLast)
            history.RemoveAt(history.Count - 1);
        history.AddRange(written);
        IndexTotals(items);
    }

    /// <summary>
    /// Merges freshly downloaded prices into the cache and prices any recorded usage that was waiting for them.
    /// Fresh prices replace cached ones they overlap.
    /// </summary>
    public void MergePrices(IEnumerable<SpotPrice> fresh)
    {
        var incoming = SpotPrices.Validate(fresh);
        var superseded = incoming.SelectMany(price => SpotPrices.Between(Prices, price.Start, price.End)).ToHashSet();
        var merged = SpotPrices.Validate(Prices.Where(price => !superseded.Contains(price)).Concat(incoming));

        AtomicFile.Write(PathOf(DataFiles.Prices), JsonSerializer.Serialize(merged, JsonOptions));
        Prices = merged;
        PriceUnpricedHistory();
    }

    public void Export(string path) => CsvExporter.Write(path, history, Settings);

    private string PathOf(string fileName) => Path.Combine(Folder, fileName);

    private ApplicationSettings LoadSettings()
    {
        var path = PathOf(DataFiles.Settings);
        if (!File.Exists(path))
            return new ApplicationSettings();

        try
        {
            var settings = JsonSerializer.Deserialize<ApplicationSettings>(File.ReadAllText(path))
                ?? throw new JsonException(Localization.Translate("Settings must be an object."));
            return SettingsValidator.Normalize(settings);
        }
        catch (Exception exception) when (IsReadError(exception))
        {
            Warn(exception);
            return new ApplicationSettings();
        }
    }

    private void LoadPrices()
    {
        var path = PathOf(DataFiles.Prices);
        if (!File.Exists(path))
            return;

        try
        {
            var prices = JsonSerializer.Deserialize<List<SpotPrice>>(File.ReadAllText(path));
            if (prices is { Count: > 0 })
                Prices = SpotPrices.Validate(prices);
        }
        catch (Exception exception) when (IsReadError(exception))
        {
            Warn(exception);
        }
    }

    private void LoadHistory()
    {
        var path = PathOf(DataFiles.Usage);
        if (!File.Exists(path))
            return;

        try
        {
            foreach (var line in File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                try
                {
                    var row = JsonSerializer.Deserialize<UsageRecord>(line);
                    if (!IsValid(row) || (history.Count > 0 && history[^1].End > row.Start))
                        throw new InvalidDataException("Invalid history interval.");
                    history.Add(row);
                }
                catch (Exception exception) when (exception is JsonException or InvalidDataException)
                {
                    Warning ??= Localization.Translate("An invalid history row was skipped.");
                }
            }
        }
        catch (Exception exception) when (IsReadError(exception))
        {
            Warn(exception);
        }
    }

    // Usage recorded while no price was known is priced again whenever new prices arrive.
    private void PriceUnpricedHistory()
    {
        var rewritten = new List<UsageRecord>(history.Count);
        var anyPriced = false;
        foreach (var row in history)
        {
            if (row.Euros.HasValue)
            {
                rewritten.Add(row);
                continue;
            }

            var watts = row.Kwh * 1000 / (row.End - row.Start).TotalHours;
            var pieces = EnergyCalculation.Split(row.Start, row.End, watts, row.ExtraCents, Prices);
            anyPriced |= pieces.Any(piece => piece.Euros.HasValue);
            rewritten.AddRange(pieces);
        }
        if (!anyPriced)
            return;

        var lines = rewritten.Select(row => JsonSerializer.Serialize(row) + Environment.NewLine);
        AtomicFile.Write(PathOf(DataFiles.Usage), string.Concat(lines));
        history.Clear();
        history.AddRange(rewritten);
        dailyTotals.Clear();
        IndexTotals(history);
    }

    private void IndexTotals(IEnumerable<UsageRecord> rows)
    {
        foreach (var row in rows)
        {
            var day = FinnishTime.ToLocal(row.Start).Date;
            dailyTotals[day] = dailyTotals.GetValueOrDefault(day, new UsageTotals()).Add(row);
        }
    }

    private static long QuarterHour(DateTimeOffset instant) => instant.UtcTicks / QuarterHourTicks;

    // Joined records stay within one quarter hour, which never straddles a price interval or Finnish midnight.
    private static bool CanJoin(UsageRecord previous, UsageRecord next) =>
        previous.End == next.Start && previous.ExtraCents == next.ExtraCents && previous.Euros.HasValue == next.Euros.HasValue &&
        QuarterHour(previous.Start) == QuarterHour(next.End - TimeSpan.FromTicks(1));

    private static List<UsageRecord> Join(UsageRecord? first, IEnumerable<UsageRecord> rows)
    {
        var joined = new List<UsageRecord>();
        if (first is not null)
            joined.Add(first);
        foreach (var row in rows)
        {
            if (joined.Count > 0 && CanJoin(joined[^1], row))
            {
                var last = joined[^1];
                joined[^1] = last with { End = row.End, Kwh = last.Kwh + row.Kwh, Euros = last.Euros + row.Euros };
            }
            else
            {
                joined.Add(row);
            }
        }
        return joined;
    }

    // Whether the file ends with a line that holds exactly this record, and where that line starts.
    private static bool EndsWithRecord(FileStream stream, UsageRecord record, out long lineStart)
    {
        lineStart = 0;
        var length = (int)Math.Min(stream.Length, TailBytes);
        if (length == 0)
            return false;

        var tail = new byte[length];
        stream.Seek(-length, SeekOrigin.End);
        stream.ReadExactly(tail);

        // The last line ends with a line break, and the break before it marks where the line starts.
        var end = length - 1;
        if (tail[end] != (byte)'\n')
            return false;
        var begin = end == 0 ? 0 : Array.LastIndexOf(tail, (byte)'\n', end - 1) + 1;
        if (begin == 0 && stream.Length > length)
            return false;

        try
        {
            lineStart = stream.Length - length + begin;
            return JsonSerializer.Deserialize<UsageRecord>(tail.AsSpan(begin, end - begin)) == record;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void Warn(Exception exception) =>
        Warning ??= Localization.Format("Unable to read saved data: {0}", exception.Message);

    private static bool IsReadError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException;

    internal static bool IsValid([NotNullWhen(true)] UsageRecord? row) =>
        row is not null && row.End > row.Start &&
        double.IsFinite(row.Kwh) && row.Kwh >= 0 && double.IsFinite(row.ExtraCents) &&
        (row.Euros is null || double.IsFinite(row.Euros.Value));
}
