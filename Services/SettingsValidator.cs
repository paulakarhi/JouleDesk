namespace JouleDesk;

internal static class SettingsValidator
{
    public static bool IsHttpsUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>Replaces every out-of-range or malformed value with its default.</summary>
    public static ApplicationSettings Normalize(ApplicationSettings settings)
    {
        var defaults = ApplicationSettings.Defaults;
        var catalogUrl = settings.CatalogUrl?.Trim() ?? "";
        return settings with
        {
            OtherWatts = Within(settings.OtherWatts, SettingLimits.OtherWatts, defaults.OtherWatts),
            Efficiency = Within(settings.Efficiency, SettingLimits.Efficiency, defaults.Efficiency),
            BoardAndCoolingWatts = Within(settings.BoardAndCoolingWatts, SettingLimits.BoardAndCoolingWatts, defaults.BoardAndCoolingWatts),
            RamWattsPerModule = Within(settings.RamWattsPerModule, SettingLimits.RamWattsPerModule, defaults.RamWattsPerModule),
            FixedPrice = Within(settings.FixedPrice, SettingLimits.FixedPrice, defaults.FixedPrice),
            Margin = Within(settings.Margin, SettingLimits.Margin, defaults.Margin),
            Transfer = Within(settings.Transfer, SettingLimits.Charge, defaults.Transfer),
            Tax = Within(settings.Tax, SettingLimits.Charge, defaults.Tax),
            PriceMode = Enum.IsDefined(settings.PriceMode) ? settings.PriceMode : defaults.PriceMode,
            CatalogUrl = catalogUrl.Length == 0 || IsHttpsUrl(catalogUrl) ? catalogUrl : ""
        };
    }

    /// <summary>Throws when the settings would be changed by <see cref="Normalize"/>.</summary>
    public static void Validate(ApplicationSettings settings)
    {
        if (settings != Normalize(settings))
            throw new InvalidDataException(Localization.Translate("Invalid application settings."));
    }

    private static double Within(double value, ValueRange range, double fallback) => range.Contains(value) ? value : fallback;
}
