using System.Net.Http.Headers;
using System.Text.Json;
using Hakari.Core.Configuration;

namespace Hakari.Core.Updates;

/// <summary>What the last look at the project's releases found.</summary>
/// <param name="Latest">The newest published version, such as "0.9.2".</param>
/// <param name="Url">Its release page.</param>
public sealed record UpdateState(DateTimeOffset CheckedAt, string Latest, string Url);

/// <summary>
/// Asks GitHub, at most once a day, for the newest published Hakari release, so a portable
/// copy can say when a newer one is out. Only the public release list is fetched; nothing
/// about the user or their usage is sent. The answer is kept in a small file both
/// processes read.
/// </summary>
public static class UpdateCheck
{
    public const string ReleasesPage = "https://github.com/PHUICMT/hakari/releases";

    private const string ReleasesAddress =
        "https://api.github.com/repos/PHUICMT/hakari/releases?per_page=20";
    private const string StateFileName = "update.json";
    private const string ProductName = "Hakari";
    private const string VersionPrefix = "v";

    private static readonly TimeSpan CheckEvery = TimeSpan.FromDays(1);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions StateOptions = new() { WriteIndented = true };

    public static string StatePath => Path.Combine(HakariPaths.DataDirectory, StateFileName);

    public static bool IsDue(DateTimeOffset now) =>
        Load() is not { } state || now - state.CheckedAt >= CheckEvery;

    /// <summary>The last answer; null before the first check or when unreadable.</summary>
    public static UpdateState? Load()
    {
        try
        {
            return File.Exists(StatePath)
                ? JsonSerializer.Deserialize<UpdateState>(File.ReadAllText(StatePath))
                : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException
            or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The newer release than <paramref name="current"/>, if one was found.</summary>
    public static UpdateState? NewerThan(Version current) =>
        Load() is { } state && TryParse(state.Latest, out var latest) && latest > current
            ? state
            : null;

    /// <summary>
    /// Fetches the release list and keeps the newest published version. A network problem
    /// is quiet: the next day tries again.
    /// </summary>
    public static async Task CheckAsync(DateTimeOffset now, CancellationToken cancellation)
    {
        try
        {
            using var client = new HttpClient { Timeout = RequestTimeout };
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue(ProductName, "1"));
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            var json = await client.GetStringAsync(ReleasesAddress, cancellation);
            if (Newest(json) is { } newest)
            {
                Save(new UpdateState(now, newest.Version, newest.Url));
            }
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException or JsonException or IOException
            or UnauthorizedAccessException)
        {
            // Tried again on a later day.
        }
    }

    /// <summary>The highest version among published releases; drafts are left out.</summary>
    public static (string Version, string Url)? Newest(string releasesJson)
    {
        using var document = JsonDocument.Parse(releasesJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        (Version Parsed, string Text, string Url)? best = null;
        foreach (var release in document.RootElement.EnumerateArray())
        {
            var isDraft = release.TryGetProperty("draft", out var draft)
                && draft.ValueKind == JsonValueKind.True;
            if (isDraft
                || !release.TryGetProperty("tag_name", out var tag)
                || tag.GetString() is not { } tagText
                || !TryParse(tagText, out var version))
            {
                continue;
            }

            var url = release.TryGetProperty("html_url", out var page)
                ? page.GetString() ?? ReleasesPage
                : ReleasesPage;
            if (best is null || version > best.Value.Parsed)
            {
                best = (version, Plain(tagText), url);
            }
        }

        return best is { } found ? (found.Text, found.Url) : null;
    }

    /// <summary>"v0.9.2" and "0.9.2" read as 0.9.2; a suffix like "-beta" is ignored.</summary>
    public static bool TryParse(string text, out Version version)
    {
        var plain = Plain(text);
        var dash = plain.IndexOf('-');
        return Version.TryParse(dash >= 0 ? plain[..dash] : plain, out version!);
    }

    private static string Plain(string text) =>
        text.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase)
            ? text[VersionPrefix.Length..]
            : text;

    private static void Save(UpdateState state)
    {
        Directory.CreateDirectory(HakariPaths.DataDirectory);
        var temporary = StatePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, StateOptions));
        File.Move(temporary, StatePath, overwrite: true);
    }
}
