namespace JouleDesk;

internal static class EmbeddedResources
{
    public const string Icon = "JouleDesk.app.ico";
    public const string Translations = "JouleDesk.locales.tsv";
    public const string DriveProfiles = "JouleDesk.drive-profiles.json";

    public static Stream Open(string name) =>
        typeof(EmbeddedResources).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"Embedded resource {name} is missing.");
}
