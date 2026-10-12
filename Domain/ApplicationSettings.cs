using System.Text.Json.Serialization;

namespace JouleDesk;

/// <summary>
/// User-adjustable assumptions. Prices are cents per kWh including VAT.
/// Acceptable ranges live in <see cref="SettingLimits"/>.
/// </summary>
internal sealed record ApplicationSettings
{
    public static ApplicationSettings Defaults { get; } = new();

    // Consumption estimates
    public bool ComponentEstimates { get; init; } = true;
    public double BoardAndCoolingWatts { get; init; } = 20;
    public double RamWattsPerModule { get; init; } = 4;
    public double OtherWatts { get; init; } = 30;
    public double Efficiency { get; init; } = 90;
    public string CatalogUrl { get; init; } = "";

    // Pricing
    public PriceMode PriceMode { get; init; } = PriceMode.SpotOnly;
    public double FixedPrice { get; init; } = 8;
    public double Margin { get; init; }
    public double Transfer { get; init; }
    public double Tax { get; init; }

    /// <summary>Contract charges added on top of the spot price.</summary>
    [JsonIgnore]
    public double Extras => Margin + Transfer + Tax;
}
