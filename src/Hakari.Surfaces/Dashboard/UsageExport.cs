using System.Diagnostics;
using System.Text;
using Hakari.Core.Accounts;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Saves what the dashboard's filter shows as a CSV file, one line per day, account, project
/// and model, then shows the file in Explorer.
/// </summary>
internal static class UsageExport
{
    private const string FileType = ".csv";

    public static async Task SaveAsync(FrameworkElement anchor)
    {
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"hakari-usage-{DateTime.Now:yyyy-MM-dd}",
            };
            picker.FileTypeChoices.Add("CSV", [FileType]);
            var window = Microsoft.UI.Win32Interop.GetWindowFromWindowId(
                anchor.XamlRoot.ContentIslandEnvironment.AppWindowId);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, window);
            if (await picker.PickSaveFileAsync() is not { } file)
            {
                return;
            }

            var filter = DashboardFilter.Current.ToUsageFilter(DateTimeOffset.Now);
            var path = file.Path;
            if (await Task.Run(() => Write(path, filter)))
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"")?.Dispose();
            }
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception, "export");
        }
    }

    private static bool Write(string path, UsageFilter filter) => DashboardData.Read(
        (query, store) =>
        {
            var settings = SettingsStore.Default.Load();
            var names = new AccountRepository(store).ListAccounts().ToDictionary(
                account => account.AccountId,
                account => AccountLabels.Full(account, settings.NicknameOf(account.AccountId)));
            using var writer = new StreamWriter(path, append: false, new UTF8Encoding(true));
            UsageCsv.Write(
                writer,
                query.Summarize(filter, GroupBy.DayAccountProjectModel),
                query.Currency,
                id => names.GetValueOrDefault(id, id));
            return true;
        },
        false);
}
