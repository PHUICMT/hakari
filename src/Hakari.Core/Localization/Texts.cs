using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Hakari.Core.Localization;

/// <summary>
/// The user-facing strings, one JSON file per language (data/locales, embedded here). A key
/// missing from the chosen language falls back to English, and a key missing from English
/// shows as the key itself, so a gap is visible but never a crash.
/// </summary>
public static class Texts
{
    public const string English = "en";
    public const string Thai = "th";

    /// <summary>The setting value that follows the Windows display language.</summary>
    public const string FollowSystem = "system";

    public static readonly IReadOnlyList<string> Languages = [English, Thai];

    private const string ResourcePrefix = "Hakari.Core.Locales.";
    private const string ResourceSuffix = ".json";

    private static readonly IReadOnlyDictionary<string, string> EnglishTexts =
        LoadLanguage(English);
    private static volatile IReadOnlyDictionary<string, string> current = EnglishTexts;
    private static volatile string currentLanguage = English;

    public static string Language => currentLanguage;

    /// <summary>Dates and month names in the chosen language; numbers stay invariant.</summary>
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(currentLanguage);

    /// <summary>Switches language. Returns true when it changed.</summary>
    public static bool Use(string? setting)
    {
        var language = Resolve(setting);
        if (language == currentLanguage)
        {
            return false;
        }

        current = language == English ? EnglishTexts : LoadLanguage(language);
        currentLanguage = language;
        return true;
    }

    public static string Get(string key) =>
        current.TryGetValue(key, out var text) || EnglishTexts.TryGetValue(key, out text)
            ? text
            : key;

    /// <summary>The text for a key that may not exist, such as a name; else null.</summary>
    public static string? Find(string key) =>
        current.TryGetValue(key, out var text) || EnglishTexts.TryGetValue(key, out text)
            ? text
            : null;

    public static string Format(string key, params object?[] values) =>
        string.Format(CultureInfo.InvariantCulture, Get(key), values);

    /// <summary>The raw table of one language, for tests and tools.</summary>
    public static IReadOnlyDictionary<string, string> LoadLanguage(string language)
    {
        var name = ResourcePrefix + language + ResourceSuffix;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        return stream is null
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    }

    private static string Resolve(string? setting)
    {
        var wanted = string.IsNullOrWhiteSpace(setting) || setting == FollowSystem
            ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            : setting;
        return Languages.Contains(wanted) ? wanted : English;
    }
}
