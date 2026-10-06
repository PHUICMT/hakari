using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Microsoft.UI.Xaml;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// How much of the cost is the main conversation and how much is subagents, the helpers Claude
/// Code starts for a task. Their responses are logged apart, as sidechains.
/// </summary>
internal sealed partial class WorkflowsPage : LoadedPage<WorkflowsData>
{
    private const int PercentScale = 100;
    private const string DayFormat = "MMM d";

    public WorkflowsPage()
        : base("dashboard.workflows")
    {
    }

    protected override WorkflowsData Read(DashboardFilter filter) => WorkflowsData.Load(filter);

    protected override UIElement Build(WorkflowsData data)
    {
        var main = data.MainThread;
        var subagents = data.Subagents;
        var total = main.Cost + subagents.Cost;
        var perResponse = subagents.Messages == 0 ? 0 : subagents.Cost / subagents.Messages;
        var tiles = DashboardTiles.Create(
        [
            DashboardTile.Create(
                Texts.Get("dashboard.workflows.main"),
                MoneyText.Format(main.Cost, data.Currency),
                Texts.Format("dashboard.workflows.share", Share(main.Cost, total))),
            DashboardTile.Create(
                Texts.Get("dashboard.workflows.subagents"),
                MoneyText.Format(subagents.Cost, data.Currency),
                Texts.Format("dashboard.workflows.share", Share(subagents.Cost, total))),
            DashboardTile.Create(
                Texts.Get("dashboard.workflows.responses"),
                subagents.Messages.ToString("N0", CultureInfo.InvariantCulture),
                Texts.Format(
                    "dashboard.workflows.each",
                    MoneyText.Format(perResponse, data.Currency))),
            LargestTile(data),
        ]);
        return DashboardCard.Create(
            Texts.Get("dashboard.workflows.title"),
            Texts.Get("dashboard.workflows.caption"),
            tiles);
    }

    private static Microsoft.UI.Xaml.Controls.Border LargestTile(WorkflowsData data) =>
        DashboardTile.Create(
            Texts.Get("dashboard.workflows.largest"),
            data.Largest is { } largest
                ? MoneyText.Format(largest.Cost, data.Currency)
                : Texts.Get("dashboard.none"),
            data.Largest?.LastSeen.ToLocalTime().ToString(DayFormat, Texts.Culture));

    private static string Share(decimal part, decimal total) =>
        PercentText.Format(total <= 0 ? 0 : (double)(part / total) * PercentScale, 0);
}
