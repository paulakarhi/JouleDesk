namespace JouleDesk;

/// <summary>
/// Energy used during one measured interval.
/// <paramref name="Euros"/> is the cost with <paramref name="ExtraCents"/> per kWh of contract charges included,
/// or null when no spot price was known for the interval.
/// </summary>
internal sealed record UsageRecord(DateTimeOffset Start, DateTimeOffset End, double Kwh, double? Euros, double ExtraCents);
