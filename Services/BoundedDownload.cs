using System.Net.Http.Headers;
using System.Text;

namespace JouleDesk;

/// <summary>
/// Downloads a text document over HTTPS within a size and time limit. Redirects are refused, so a document
/// cannot be bounced to another host than the one asked for.
/// </summary>
internal sealed class BoundedDownload : IDisposable
{
    private const int ChunkBytes = 8192;

    private readonly HttpClient client;
    private readonly int maxBytes;
    private readonly TimeSpan timeout;

    /// <param name="maxBytes">The largest body accepted.</param>
    /// <param name="timeout">Time allowed for the whole download, body included.</param>
    /// <param name="userAgent">Product name and version sent with every request, or null to send none.</param>
    /// <param name="handler">Replaces the network, for tests.</param>
    public BoundedDownload(int maxBytes, TimeSpan timeout, ProductInfoHeaderValue? userAgent = null, HttpMessageHandler? handler = null)
    {
        this.maxBytes = maxBytes;
        this.timeout = timeout;
        client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        if (userAgent is not null)
            client.DefaultRequestHeaders.UserAgent.Add(userAgent);
    }

    public void Dispose() => client.Dispose();

    public async Task<string> ReadTextAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException(Localization.Translate("Only HTTPS addresses are accepted."));

        // HttpClient.Timeout stops at the response headers, so the deadline covers the body here.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maxBytes)
            throw TooLarge();

        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var body = new MemoryStream();
        var chunk = new byte[ChunkBytes];
        int read;
        while ((read = await stream.ReadAsync(chunk, deadline.Token).ConfigureAwait(false)) > 0)
        {
            if (body.Length + read > maxBytes)
                throw TooLarge();
            body.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(body.GetBuffer(), 0, (int)body.Length);
    }

    private static InvalidDataException TooLarge() => new(Localization.Translate("The download exceeds the size limit."));
}
