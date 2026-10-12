namespace JouleDesk;

/// <summary>Names of the files kept in the per-user data folder.</summary>
internal static class DataFiles
{
    public const string Settings = "settings.json";
    public const string Prices = "prices.json";
    public const string Usage = "usage.jsonl";
    public const string Components = "components.json";
    public const string ComponentSources = "component-sources.txt";

    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JouleDesk");
}
