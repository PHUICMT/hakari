using System.Text.Json;
using System.Text.Json.Serialization;
using Hakari.Core.Configuration;

namespace Hakari.Core.Settings;

/// <summary>
/// Settings as JSON in the data folder. A missing or unreadable file means defaults, so a bad
/// edit can never stop Hakari from starting. Writes go through a temporary file.
/// </summary>
public sealed class SettingsStore(string path)
{
    public const string FileName = "settings.json";
    private const string TemporarySuffix = ".tmp";
    private const int ReadAttempts = 4;
    private static readonly TimeSpan ReadRetryDelay = TimeSpan.FromMilliseconds(25);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static SettingsStore Default { get; } =
        new(System.IO.Path.Combine(HakariPaths.DataDirectory, FileName));

    public string Path { get; } = path;

    public HakariSettings Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return new HakariSettings();
            }

            return JsonSerializer.Deserialize<HakariSettings>(ReadShared(), SerializerOptions)
                ?? new HakariSettings();
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return new HakariSettings();
        }
    }

    /// <summary>
    /// Two processes share this file, so a read can land in the middle of the other one's
    /// replace; it is tried again briefly rather than falling back to defaults.
    /// </summary>
    private string ReadShared()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return File.ReadAllText(Path);
            }
            catch (IOException) when (attempt < ReadAttempts)
            {
                Thread.Sleep(ReadRetryDelay);
            }
        }
    }

    public void Save(HakariSettings settings)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = Path + TemporarySuffix;
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, SerializerOptions));
        File.Move(temporaryPath, Path, overwrite: true);
    }

    public HakariSettings Update(Func<HakariSettings, HakariSettings> change)
    {
        var updated = change(Load());
        Save(updated);
        return updated;
    }
}
