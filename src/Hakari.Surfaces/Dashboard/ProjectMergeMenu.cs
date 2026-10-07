using Hakari.Core.Localization;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// What a click on a project row offers: join it into another project, for a folder moved or
/// copied to a new path, and split back out any folder joined into it. Folders of the same
/// name come first, since a move keeps the name; nothing is joined without being asked.
/// </summary>
internal static class ProjectMergeMenu
{
    private const int MostTargets = 12;
    private const double ListWidth = 360;
    private const double SectionSize = 11;
    private static readonly Thickness SectionMargin = new(12, 8, 12, 4);

    /// <param name="others">The other projects shown, the costliest first.</param>
    /// <param name="changed">Runs after a join or split was saved.</param>
    public static void Show(
        FrameworkElement anchor,
        string project,
        IReadOnlyList<string> others,
        Action changed)
    {
        var merges = new ProjectMerges(SettingsStore.Default.Load().ProjectMerges);
        var list = new StackPanel { Width = ListWidth };
        var flyout = new Microsoft.UI.Xaml.Controls.Flyout
        {
            Content = list,
            Placement = FlyoutPlacementMode.Bottom,
            FlyoutPresenterStyle = (Style)Application.Current.Resources["HakariListPresenter"],
            AreOpenCloseAnimationsEnabled =
                SurfaceMotion.Current() != AnimationSetting.Off,
        };

        var joined = merges.JoinedInto(project);
        if (joined.Count > 0)
        {
            list.Children.Add(Section(Texts.Get("dashboard.merge.joined")));
            foreach (var path in joined)
            {
                list.Children.Add(Item(
                    Texts.Format("dashboard.merge.split", RowNames.Project(path).Title),
                    path,
                    () => Save(current => Without(current, path), flyout, changed)));
            }
        }

        var name = RowNames.Project(project).Title;
        var targets = others
            .Where(other => other != project)
            .OrderByDescending(other => RowNames.Project(other).Title == name)
            .Take(MostTargets)
            .ToList();
        list.Children.Add(Section(Texts.Get("dashboard.merge.into")));
        if (targets.Count == 0)
        {
            list.Children.Add(Section(Texts.Get("dashboard.merge.none")));
        }

        foreach (var target in targets)
        {
            var (title, detail) = RowNames.Project(target);
            list.Children.Add(Item(
                title,
                detail,
                () => Save(current => With(current, project, target), flyout, changed)));
        }

        flyout.ShowAt(anchor);
    }

    private static Dictionary<string, string> With(
        IReadOnlyDictionary<string, string> current,
        string from,
        string into) =>
        new(current) { [from] = into };

    private static Dictionary<string, string> Without(
        IReadOnlyDictionary<string, string> current,
        string path)
    {
        var merges = new Dictionary<string, string>(current);
        merges.Remove(path);
        return merges;
    }

    private static void Save(
        Func<IReadOnlyDictionary<string, string>, Dictionary<string, string>> change,
        Microsoft.UI.Xaml.Controls.Flyout flyout,
        Action changed)
    {
        flyout.Hide();
        SettingsStore.Default.Update(current => current with
        {
            ProjectMerges = change(current.ProjectMerges),
        });
        changed();
    }

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = SectionSize,
        Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
        Margin = SectionMargin,
        TextWrapping = TextWrapping.Wrap,
    };

    private static Button Item(string title, string? detail, Action pick)
    {
        var button = new Button
        {
            Content = TableCells.TwoLines(title, detail),
            Style = (Style)Application.Current.Resources["HakariListItemButton"],
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        button.Click += (_, _) => pick();
        return button;
    }
}
