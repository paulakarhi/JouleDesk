using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace JouleDesk;

/// <summary>Downloads Finnish spot prices from the Pörssisähkö.net public API.</summary>
internal static class SpotPriceClient
{
    private const string Endpoint = "https://api.porssisahko.net/v2/latest-prices.json";
    private static readonly HttpClient Client = CreateClient();

    public static async Task<IReadOnlyList<SpotPrice>> FetchAsync(CancellationToken cancellationToken = default)
    {
        var json = await Client.GetStringAsync(Endpoint, cancellationToken);
        return Parse(json);
    }

    public static IReadOnlyList<SpotPrice> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var prices = document.RootElement.GetProperty("prices").EnumerateArray().Select(item =>
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
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1";
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JouleDesk", version));
        return client;
    }
}
