namespace JouleDesk;

/// <summary>
/// The energy used during one minute and what it cost under the selected plan. <paramref name="Recorded"/> is the part of
/// the minute that was monitored, which is shorter than a minute when monitoring started or stopped inside it.
/// </summary>
internal sealed record MinuteUsage(DateTimeOffset Start, double Kwh, double Euros, double MissingKwh, TimeSpan Recorded)
{
    public DateTimeOffset End => Start + UsageBuckets.Minute;

    public double WattHours => Kwh * 1000;

    /// <summary>Average power over the recorded part of the minute.</summary>
    public double Watts => WattHours / Recorded.TotalHours;

    public bool IsComplete => Recorded >= UsageBuckets.Minute;
}

/// <summary>The figures shown above a consumption chart for a stretch of minutes.</summary>
internal sealed record ConsumptionSummary(double Kwh, double Euros, double MissingKwh, double? AverageWatts, MinuteUsage? Peak)
{
    public static ConsumptionSummary Of(IReadOnlyCollection<MinuteUsage> minutes)
    {
        var recorded = TimeSpan.FromTicks(minutes.Sum(minute => minute.Recorded.Ticks));
        var kwh = minutes.Sum(minute => minute.Kwh);
        return new ConsumptionSummary(
            kwh,
            minutes.Sum(minute => minute.Euros),
            minutes.Sum(minute => minute.MissingKwh),
            recorded > TimeSpan.Zero ? kwh * 1000 / recorded.TotalHours : null,
            minutes.MaxBy(minute => minute.Watts));
    }
}

/// <summary>
/// The recorded usage of one Finnish calendar day, minute by minute. Usage that was stored in longer records,
/// such as the quarter hours of older days, is spread evenly over the minutes it covers.
/// Minutes in which nothing was recorded are absent.
/// </summary>
internal sealed class DayConsumption
{
    private readonly MinuteUsage?[] slots;

    private DayConsumption(DateTime date, DateTimeOffset start, DateTimeOffset end, MinuteUsage?[] slots)
    {
        Date = date;
        Start = start;
        End = end;
        this.slots = slots;
        Minutes = slots.OfType<MinuteUsage>().ToList();
        Summary = ConsumptionSummary.Of(Minutes);
        LowestWatts = Minutes.Count == 0 ? 0 : Minutes.Min(minute => minute.Watts);
        HighestWatts = Summary.Peak?.Watts ?? 0;
    }

    public DateTime Date { get; }

    /// <summary>The instant the day begins. The day is 23 or 25 hours long when the clocks change.</summary>
    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    /// <summary>The minutes with recorded usage, in time order.</summary>
    public IReadOnlyList<MinuteUsage> Minutes { get; }

    public ConsumptionSummary Summary { get; }

    /// <summary>The lowest and highest power of the day, which the charts use to colour the minutes.</summary>
    public double LowestWatts { get; }

    public double HighestWatts { get; }

    /// <summary>The number of whole hours in the day.</summary>
    public int HourCount => slots.Length / 60;

    public static DayConsumption Create(DateTime date, IEnumerable<UsageRecord> records, ApplicationSettings settings)
    {
        var day = date.Date;
        var start = FinnishTime.ToInstant(day);
        var end = FinnishTime.ToInstant(day.AddDays(1));
        var count = (int)(end - start).TotalMinutes;
        var totals = new UsageTotals?[count];
        var recordedTicks = new long[count];

        foreach (var piece in records.SelectMany(record => UsageBuckets.Cut(record, UsageBuckets.Minute)))
        {
            if (piece.Start < start || piece.End > end)
                continue;

            var index = SlotOf(piece.Start, start);
            totals[index] = (totals[index] ?? new UsageTotals()).Add(piece);
            recordedTicks[index] += (piece.End - piece.Start).Ticks;
        }

        var slots = new MinuteUsage?[count];
        for (var index = 0; index < count; index++)
        {
            if (totals[index] is { } sum)
                slots[index] = new MinuteUsage(start + UsageBuckets.Minute * index, sum.Kwh, sum.Cost(settings), sum.MissingKwh(settings),
                    TimeSpan.FromTicks(recordedTicks[index]));
        }
        return new DayConsumption(day, start, end, slots);
    }

    /// <summary>The minute that holds an instant, or null when nothing was recorded then.</summary>
    public MinuteUsage? MinuteAt(DateTimeOffset instant) =>
        instant >= Start && instant < End ? slots[SlotOf(instant, Start)] : null;

    /// <summary>The recorded minutes that lie within the span from <paramref name="start"/> up to <paramref name="end"/>.</summary>
    public IReadOnlyList<MinuteUsage> MinutesBetween(DateTimeOffset start, DateTimeOffset end)
    {
        var first = Math.Max(0, SlotOf(start < Start ? Start : start, Start));
        var last = Math.Min(slots.Length, (int)Math.Ceiling((end - Start) / UsageBuckets.Minute));
        return slots.Skip(first).Take(Math.Max(0, last - first)).OfType<MinuteUsage>().ToList();
    }

    /// <summary>The instant the hour with the given number, counted from the start of the day, begins.</summary>
    public DateTimeOffset HourStart(int hour) => Start.AddHours(hour);

    /// <summary>The number of the hour that holds an instant, counted from the start of the day.</summary>
    public int HourOf(DateTimeOffset instant) => Math.Clamp((int)(instant - Start).TotalHours, 0, Math.Max(0, HourCount - 1));

    private static int SlotOf(DateTimeOffset instant, DateTimeOffset start) => (int)((instant - start).Ticks / UsageBuckets.Minute.Ticks);
}
