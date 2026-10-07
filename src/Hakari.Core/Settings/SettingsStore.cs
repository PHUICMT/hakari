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

    public HakariSettings Load() => TryLoad(out _);

    /// <param name="readable">
    /// False when the file exists but could not be read just now (locked, denied), so its
    /// content is unknown; a broken file reads as defaults and counts as readable.
    /// </param>
    private HakariSettings TryLoad(out bool readable)
    {
        readable = true;
        try
        {
            if (!File.Exists(Path))
            {
                return new HakariSettings();
            }

            return Normalized(
                JsonSerializer.Deserialize<HakariSettings>(ReadShared(), SerializerOptions)
                ?? new HakariSettings());
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            readable = false;
            return new HakariSettings();
        }
        catch (Exception exception) when (exception is JsonException
            or NotSupportedException)
        {
            return new HakariSettings();
        }
    }

    /// <summary>A hand-edited file may say null for a list; that reads as an empty one.</summary>
    private static HakariSettings Normalized(HakariSettings settings) => settings with
    {
        Widget = settings.Widget ?? new(),
        Dashboard = settings.Dashboard ?? new(),
        SavedLayouts = settings.SavedLayouts ?? [],
        AccountLayouts = settings.AccountLayouts
            ?? new Dictionary<string, Presentation.Widget.WidgetLayout>(),
        CustomAccountOrder = settings.CustomAccountOrder ?? [],
        CollapsedAccounts = settings.CollapsedAccounts ?? [],
        CollapsedSettingsSections = settings.CollapsedSettingsSections ?? [],
        FoldedDashboardSections = settings.FoldedDashboardSections ?? [],
        HiddenAccounts = settings.HiddenAccounts ?? [],
        PlanPriceOverrides = settings.PlanPriceOverrides ?? new Dictionary<string, decimal>(),
        AccountNicknames = settings.AccountNicknames ?? new Dictionary<string, string>(),
        ProjectMerges = settings.ProjectMerges ?? new Dictionary<string, string>(),
        ChosenDisplays = settings.ChosenDisplays ?? [],
        ExtraConfigDirectories = settings.ExtraConfigDirectories ?? [],
        LimitsOffAccounts = settings.LimitsOffAccounts ?? [],
        LimitsAskedAccounts = settings.LimitsAskedAccounts ?? [],
        Language = settings.Language ?? Localization.Texts.FollowSystem,
        Currency = settings.Currency ?? Currency.CurrencyCodes.Dollar,
        LastSeenVersion = settings.LastSeenVersion ?? string.Empty,
    };

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
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException && attempt < ReadAttempts)
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

        // Each writer has its own temporary file, so the two processes never write into the
        // same one; a replace that meets the other process reading is tried again, and one
        // that still fails is dropped rather than taking the caller down.
        var temporaryPath = $"{Path}.{Environment.ProcessId}.{Guid.NewGuid():N}{TemporarySuffix}";
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        for (var attempt = 1; attempt <= ReadAttempts; attempt++)
        {
            try
            {
                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, Path, overwrite: true);
                return;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException)
            {
                Thread.Sleep(ReadRetryDelay);
            }
        }

        try
        {
            File.Delete(temporaryPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            // Left for the next start to overwrite; it is never read.
        }
    }

    /// <summary>
    /// Reads, changes and writes as one step across both processes, so neither loses the
    /// other's change. When the file could not be read, nothing is written: writing the
    /// defaults over it would wipe the user's settings.
    /// </summary>
    public HakariSettings Update(Func<HakariSettings, HakariSettings> change)
    {
        using var turn = new Mutex(initiallyOwned: false, UpdateMutexName);
        var owned = WaitForTurn(turn);
        try
        {
            var updated = change(TryLoad(out var readable));
            if (readable)
            {
                Save(updated);
            }

            return updated;
        }
        finally
        {
            if (owned)
            {
                turn.ReleaseMutex();
            }
        }
    }

    private const string UpdateMutexName = @"Local\Hakari.Settings.Update";
    private static readonly TimeSpan UpdateWait = TimeSpan.FromSeconds(2);

    /// <summary>A crashed holder leaves the mutex abandoned; that still counts as a turn.</summary>
    private static bool WaitForTurn(Mutex turn)
    {
        try
        {
            return turn.WaitOne(UpdateWait);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }
}
