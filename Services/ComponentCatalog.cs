using System.Text;
using System.Text.Json;

namespace JouleDesk;

/// <summary>
/// Power profiles of drive models: the bundled manufacturer data, optionally extended by a catalog
/// the user downloads from their own HTTPS address and caches in the data folder.
/// </summary>
internal sealed class ComponentCatalog
{
    private const int SupportedVersion = 1;
    private const int MaxProfiles = 10_000;
    private const int MaxModelLength = 200;
    private const int MaxDownloadBytes = 2_000_000;
    private static readonly ValueRange PowerLimit = new(0, 100);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    // Redirects are refused so a catalog cannot be bounced to a different host than the one configured.
    private static readonly HttpClient Downloader = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = DownloadTimeout
    };

    private static readonly DiskProfile[] Bundled = ReadBundledProfiles();

    // Replaced as a whole on update, so the sensor thread can keep reading while the UI thread applies a new catalog.
    private volatile IReadOnlyList<DiskProfile> profiles = Bundled;

    public string Status { get; private set; } =
        Localization.Format("Bundled manufacturer data · {0} drive models", Bundled.Length);

    public DiskProfile? Find(string model)
    {
        var name = model.Trim();
        return profiles.FirstOrDefault(profile => string.Equals(profile.Model.Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Applies the catalog cached by an earlier update, if there is one.</summary>
    public void LoadCache(string folder)
    {
        var path = Path.Combine(folder, DataFiles.Components);
        if (!File.Exists(path))
            return;

        try
        {
            Apply(File.ReadAllText(path));
            Status = Localization.Translate("Cached component database");
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Status = Localization.Translate("Database read failed · using bundled data");
        }
    }

    /// <summary>Validates a catalog and merges it over the bundled profiles. Later entries win.</summary>
    public void Apply(string json) => Merge(Validate(json));

    public async Task UpdateAsync(string url, string folder, CancellationToken cancellationToken)
    {
        if (!SettingsValidator.IsHttpsUrl(url))
            throw new InvalidDataException(Localization.Translate("Enter an HTTPS catalog URL."));

        var json = await DownloadAsync(new Uri(url), cancellationToken);
        var document = Validate(json);
        cancellationToken.ThrowIfCancellationRequested();

        AtomicFile.Write(Path.Combine(folder, DataFiles.Components), json);
        Merge(document);
        Status = Localization.Format("Updated {0} · {1} drive models", FinnishTime.Now.ToString(DisplayFormat.DayAndTime), profiles.Count);
    }

    public static CatalogDocument Validate(string json)
    {
        var document = JsonSerializer.Deserialize<CatalogDocument>(json, ReadOptions);
        if (document is null || document.Version != SupportedVersion || document.Disks is null || document.Disks.Count > MaxProfiles)
            throw new InvalidDataException(Localization.Translate("Unknown catalog format."));

        foreach (var disk in document.Disks)
            ValidateProfile(disk);

        var distinctModels = document.Disks.Select(disk => disk.Model.Trim().ToUpperInvariant()).Distinct().Count();
        if (distinctModels != document.Disks.Count)
            throw new InvalidDataException(Localization.Translate("Duplicate drive model."));
        return document;
    }

    private static void ValidateProfile(DiskProfile? disk)
    {
        if (disk is null || string.IsNullOrWhiteSpace(disk.Model) || disk.Model.Length > MaxModelLength ||
            !SettingsValidator.IsHttpsUrl(disk.Source) || !HasPlausiblePower(disk))
            throw new InvalidDataException(Localization.Translate("Invalid component power values or source."));
    }

    // Idle is the floor; read, write and the typical figure must not undercut it, and typical cannot exceed the busy figures.
    private static bool HasPlausiblePower(DiskProfile disk) =>
        new[] { disk.IdleWatts, disk.ReadWatts, disk.WriteWatts, disk.TypicalWatts }.All(PowerLimit.Contains) &&
        disk.ReadWatts >= disk.IdleWatts && disk.WriteWatts >= disk.IdleWatts &&
        disk.TypicalWatts >= disk.IdleWatts && disk.TypicalWatts <= Math.Max(disk.ReadWatts, disk.WriteWatts);

    private void Merge(CatalogDocument document) =>
        profiles = Bundled.Concat(document.Disks)
            .GroupBy(profile => profile.Model.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

    private static async Task<string> DownloadAsync(Uri uri, CancellationToken cancellationToken)
    {
        // HttpClient.Timeout stops at the response headers, so the body gets its own deadline.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DownloadTimeout);

        using var response = await Downloader.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
        {
            if (buffer.Length + read > MaxDownloadBytes)
                throw new InvalidDataException(Localization.Translate("Catalog exceeds the size limit."));
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static DiskProfile[] ReadBundledProfiles()
    {
        using var reader = new StreamReader(EmbeddedResources.Open(EmbeddedResources.DriveProfiles));
        return Validate(reader.ReadToEnd()).Disks.ToArray();
    }
}
