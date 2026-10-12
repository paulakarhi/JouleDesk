namespace JouleDesk;

/// <summary>
/// Spot price in cents per kWh (VAT included) for the interval from <paramref name="Start"/> up to,
/// but not including, <paramref name="End"/>.
/// </summary>
internal sealed record SpotPrice(double PriceCents, DateTimeOffset Start, DateTimeOffset End);
