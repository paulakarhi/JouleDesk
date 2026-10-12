namespace JouleDesk;

/// <summary>
/// Running sums over usage records. Keeping the spot cost separate from the recorded fees lets
/// the selected tariff be applied afterwards without rescanning the records.
/// </summary>
internal sealed record UsageTotals(double Kwh = 0, double PricedKwh = 0, double SpotEuros = 0)
{
    private const double RoundingTolerance = 1e-12;

    public UsageTotals Add(UsageRecord row) => new(
        Kwh + row.Kwh,
        PricedKwh + (row.Euros.HasValue ? row.Kwh : 0),
        SpotEuros + (TariffCalculator.SpotCost(row) ?? 0));

    public UsageTotals Add(UsageTotals other) => new(
        Kwh + other.Kwh,
        PricedKwh + other.PricedKwh,
        SpotEuros + other.SpotEuros);

    /// <summary>Energy that cannot be priced yet. Fixed-price plans never miss prices.</summary>
    public double MissingKwh(ApplicationSettings settings)
    {
        // Sums taken in different orders differ by rounding error, which is not missing energy.
        var missing = Kwh - PricedKwh;
        return settings.PriceMode == PriceMode.Fixed || missing < RoundingTolerance ? 0 : missing;
    }

    public double Cost(ApplicationSettings settings) => TariffCalculator.Cost(this, settings);
}
