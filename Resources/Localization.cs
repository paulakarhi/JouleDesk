using System.Globalization;
using Microsoft.Win32;

namespace JouleDesk;

/// <summary>
/// Interface translations. The English text is the lookup key; translated text lives in the
/// <c>locales.tsv</c> resource, one column per language. Text that is not a key is returned unchanged,
/// so values that are already translated or computed can be passed through freely.
/// </summary>
internal static class Localization
{
    // Column order of locales.tsv, with the culture each language uses for numbers and dates.
    private static readonly (string Code, string Culture)[] Languages =
    {
        ("fi", "fi-FI"), ("en", "en-GB"), ("sv", "sv-SE"), ("de", "de-DE"), ("fr", "fr-FR"), ("es", "es-ES"),
        ("pt", "pt-PT"), ("it", "it-IT"), ("ru", "ru-RU"), ("zh", "zh-CN"), ("ja", "ja-JP"), ("ko", "ko-KR")
    };

    // The English column is the lookup key and the fallback for unsupported languages.
    private const int EnglishColumn = 1;
    private const string SettingsKeyPath = @"Software\JouleDesk";
    private const string LanguageValueName = "Language";

    private static readonly Dictionary<string, string[]> Entries = ReadEntries();
    private static int column = EnglishColumn;

    public static IReadOnlyList<string> Codes { get; } = Languages.Select(language => language.Code).ToArray();

    public static string Language => Languages[column].Code;

    /// <summary>All translations by English key, one value per language in <see cref="Codes"/> order.</summary>
    public static IReadOnlyDictionary<string, string[]> Table => Entries;

    /// <summary>
    /// Selects the language chosen in the installer, which stores it in the registry.
    /// Without one, the Windows display language is used when supported, otherwise English.
    /// </summary>
    public static void Initialize()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
        Set(key?.GetValue(LanguageValueName) as string ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }

    public static void Set(string language)
    {
        column = Array.FindIndex(Languages, candidate => candidate.Code == language);
        if (column < 0)
            column = EnglishColumn;

        var culture = CultureInfo.GetCultureInfo(Languages[column].Culture);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static string Translate(string key)
    {
        if (Entries.TryGetValue(key, out var values))
            return values[column];

        // Multi-line captions are translated line by line.
        return key.Contains('\n') ? string.Join('\n', key.Split('\n').Select(Translate)) : key;
    }

    public static string Format(string key, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, Translate(key), values);

    private static Dictionary<string, string[]> ReadEntries()
    {
        using var reader = new StreamReader(EmbeddedResources.Open(EmbeddedResources.Translations));
        var entries = new Dictionary<string, string[]>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            var values = line.Split('|').Select(value => value.Replace("\\n", "\n")).ToArray();
            if (values.Length != Languages.Length)
                throw new InvalidDataException($"Translation row has {values.Length} columns: {line}");
            entries.Add(values[EnglishColumn], values);
        }
        return entries;
    }
}
