using System.Text.RegularExpressions;
using Hakari.Core.Localization;

namespace Hakari.Core.Tests.Localization;

public sealed partial class LocaleFileTests
{
    /// <summary>Guards against the scan silently matching nothing.</summary>
    private const int MinimumKeysExpectedInSource = 100;

    private static readonly IReadOnlyDictionary<string, string> English =
        Texts.LoadLanguage(Texts.English);

    public static TheoryData<string> OtherLanguages =>
        [.. Texts.Languages.Where(language => language != Texts.English)];

    [Fact]
    public void English_has_texts() => Assert.NotEmpty(English);

    [Theory]
    [MemberData(nameof(OtherLanguages))]
    public void Every_language_has_exactly_the_english_keys(string language)
    {
        var keys = Texts.LoadLanguage(language).Keys.ToHashSet();

        Assert.Empty(English.Keys.Except(keys));
        Assert.Empty(keys.Except(English.Keys));
    }

    [Theory]
    [MemberData(nameof(OtherLanguages))]
    public void Placeholders_match_english(string language)
    {
        var texts = Texts.LoadLanguage(language);
        var mismatched = English
            .Where(entry => texts.TryGetValue(entry.Key, out var translated)
                && Placeholders(translated) != Placeholders(entry.Value))
            .Select(entry => entry.Key);

        Assert.Empty(mismatched);
    }

    /// <summary>A key typed in code or XAML but missing from the files would show raw.</summary>
    [Fact]
    public void Every_key_used_in_source_exists()
    {
        var used = SourceFiles()
            .SelectMany(file => KeyPattern().Matches(File.ReadAllText(file)))
            .Select(match => match.Groups["key"].Value)
            .Where(key => !key.EndsWith(".json", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.True(used.Count > MinimumKeysExpectedInSource, $"found only {used.Count}");
        var missing = used.Where(key => !English.ContainsKey(key)).ToList();
        Assert.Empty(missing);
    }

    private static string Placeholders(string text) =>
        string.Join(',', PlaceholderPattern().Matches(text)
            .Select(match => match.Value)
            .Order(StringComparer.Ordinal));

    private static IEnumerable<string> SourceFiles()
    {
        var source = Path.Combine(RepositoryRoot(), "src");
        return Directory.EnumerateFiles(source, "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal)
                || file.EndsWith(".xaml", StringComparison.Ordinal))
            .Where(file => !IsBuildOutput(file));
    }

    private static bool IsBuildOutput(string file)
    {
        var separator = Path.DirectorySeparatorChar;
        return file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
            || file.Contains($"{separator}bin{separator}", StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("src");
    }

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"[""=](?<key>(widget|limit|reset|age|plan|account|flyout|menu|settings)"
        + @"\.[A-Za-z.]+)[""}]")]
    private static partial Regex KeyPattern();
}
