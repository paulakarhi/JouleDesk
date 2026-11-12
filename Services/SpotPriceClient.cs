using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace JouleDesk;

/// <summary>Downloads Finnish spot prices from the Pörssisähkö.net public API.</summary>
internal static class SpotPriceClient
{
    private const string Endpoint = "https://api.porssisahko.net/v2/latest-prices.json";
    private const string ProductName = "JouleDesk";
    private const int MaxResponseBytes = 1_000_000;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private static readonly BoundedDownload Download = new(MaxResponseBytes, Timeout, CreateUserAgent());

    public static async Task<IReadOnlyList<SpotPrice>> FetchAsync(CancellationToken cancellationToken = default) =>
        Parse(await Download.ReadTextAsync(new Uri(Endpoint), cancellationToken));

    public static IReadOnlyList<SpotPrice> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var items = document.RootElement.GetProperty("prices");
        if (items.GetArrayLength() > PriceLimits.MaxPerDownload)
            throw new InvalidDataException(Localization.Translate("The API returned too many prices."));

        var prices = items.EnumerateArray().Select(item =>
        {
            var end = item.GetProperty("endDate").GetDateTimeOffset();

            // The API reports the last millisecond of an interval as its end; intervals here end exclusively.
            if (end.Millisecond == 999)
                end = end.AddMilliseconds(1);

            return new SpotPrice(item.GetProperty("price").GetDouble(), item.GetProperty("startDate").GetDateTimeOffset(), end);
        });
        return SpotPrices.Validate(prices);
    }

    // The service is run by one person; naming the application lets them see what uses it.
    private static ProductInfoHeaderValue CreateUserAgent() =>
        new(ProductName, Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1");
}
