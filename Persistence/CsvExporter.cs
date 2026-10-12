using System.Globalization;
using System.Text;

namespace JouleDesk;

/// <summary>
/// Writes the recorded intervals priced with the current plan. The format does not depend on the interface
/// language: English headers, invariant decimal points and UTC times, which stay unambiguous across daylight saving.
/// </summary>
internal static class CsvExporter
{
    private const string Header = "start_utc;end_utc;energy_kWh;cost_EUR;price_mode;fixed_cents_kWh;margin_cents_kWh;transfer_cents_kWh;tax_cents_kWh";

    public static void Write(string path, IEnumerable<UsageRecord> history, ApplicationSettings settings)
    {
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine(Header);
        foreach (var row in history)
        {
            writer.WriteLine(string.Join(';',
                row.Start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                row.End.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                Number(row.Kwh),
                Number(TariffCalculator.Cost(row, settings)),
                settings.PriceMode,
                Number(settings.FixedPrice),
                Number(settings.Margin),
                Number(settings.Transfer),
                Number(settings.Tax)));
        }
    }

    // An unknown cost is written as an empty field, not as zero.
    private static string Number(double? value) => value?.ToString("0.########", CultureInfo.InvariantCulture) ?? "";
}
