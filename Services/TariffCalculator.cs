namespace JouleDesk;

internal static class TariffCalculator
{
    /// <summary>Display name of a price mode, as a translation key.</summary>
    public static string Name(PriceMode mode) => mode switch
    {
        PriceMode.SpotOnly => "Spot price only",
        PriceMode.SpotWithFees => "Spot + contract charges",
        _ => "Fixed price + distribution and tax"
    };

    /// <summary>Price per kWh under the selected plan. Null when the plan needs a spot price that is not known.</summary>
    public static double? Cents(double? spot, ApplicationSettings settings) => settings.PriceMode switch
    {
        PriceMode.SpotOnly => spot,
        PriceMode.SpotWithFees => spot + settings.Extras,
        _ => FixedCents(settings)
    };

    /// <summary>
    /// Price shown for the market itself. Contract charges are included only when the plan has them;
    /// a fixed-price plan still shows the plain spot price for reference.
    /// </summary>
    public static double MarketCents(double spot, ApplicationSettings settings) =>
        settings.PriceMode == PriceMode.SpotWithFees ? spot + settings.Extras : spot;

    /// <summary>Spot cost of a record without the contract charges it was recorded with.</summary>
    public static double? SpotCost(UsageRecord row) => row.Euros - row.Kwh * row.ExtraCents / 100;

    public static double? Cost(UsageRecord row, ApplicationSettings settings)
    {
        if (row.Euros is null && settings.PriceMode != PriceMode.Fixed)
            return null;
        return Cost(new UsageTotals().Add(row), settings);
    }

    public static double Cost(UsageTotals totals, ApplicationSettings settings) => settings.PriceMode switch
    {
        PriceMode.Fixed => totals.Kwh * FixedCents(settings) / 100,
        PriceMode.SpotWithFees => totals.SpotEuros + totals.PricedKwh * settings.Extras / 100,
        _ => totals.SpotEuros
    };

    private static double FixedCents(ApplicationSettings settings) => settings.FixedPrice + settings.Transfer + settings.Tax;
}
