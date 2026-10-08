using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// What a project's menu button offers: a monthly budget; joining it into another project,
/// for a folder moved or copied to a new path; and splitting back out any folder joined into
/// it. Folders of the same name come first, since a move keeps the name. Picking one only
/// asks: the menu turns into a question saying what will change, and nothing is saved until
/// that is confirmed.
/// </summary>
internal static class ProjectMergeMenu
{
    private const int MostTargets = 12;
    private const double ListWidth = 360;
    private const double SectionSize = 11;
    private static readonly Thickness SectionMargin = new(12, 8, 12, 4);
    private const double SwapDistance = 24;
    private const int BudgetLength = 12;
    private const double QuestionSize = 14;
    private const double ExplainSize = 12;
    private const double QuestionSpacing = 10;
    private static readonly Thickness QuestionPadding = new(12, 10, 12, 8);
    private static readonly Thickness FolderPadding = new(10, 8, 10, 8);

    /// <param name="others">The other projects shown, the costliest first.</param>
    /// <param name="currency">The currency a budget is typed in.</param>
    /// <param name="changed">Runs after a budget, join or split was saved.</param>
    public static void Show(
        FrameworkElement anchor,
        string project,
        IReadOnlyList<string> others,
        string currency,
        Action changed)
    {
        var settings = SettingsStore.Default.Load();
        var merges = new ProjectMerges(settings.ProjectMerges);
        var list = new StackPanel { Width = ListWidth };
        var flyout = new Microsoft.UI.Xaml.Controls.Flyout
        {
            Content = list,
            Placement = FlyoutPlacementMode.Bottom,
            FlyoutPresenterStyle = (Style)Application.Current.Resources["HakariListPresenter"],
        };
        SurfaceMotion.EnterOnOpen(flyout);

        void Ask(UIElement question)
        {
            flyout.Content = question;
            SurfaceMotion.Enter(question, fromX: SwapDistance);
        }

        void Back()
        {
            flyout.Content = list;
            SurfaceMotion.Enter(list, fromX: -SwapDistance);
        }

        var name = RowNames.Project(project).Title;
        var budget = settings.ProjectBudgets.GetValueOrDefault(project);
        list.Children.Add(Section(Texts.Get("dashboard.budget.section")));
        list.Children.Add(Item(
            budget > 0
                ? Texts.Format("dashboard.budget.edit", MoneyText.Format(budget, currency))
                : Texts.Get("dashboard.budget.set"),
            null,
            () => Ask(BudgetQuestion(
                name,
                budget,
                currency,
                amount => SaveBudget(project, amount, flyout, changed),
                Back))));

        var joined = merges.JoinedInto(project);
        if (joined.Count > 0)
        {
            list.Children.Add(Section(Texts.Get("dashboard.merge.joined")));
            foreach (var path in joined)
            {
                list.Children.Add(Item(
                    Texts.Format("dashboard.merge.split", RowNames.Project(path).Title),
                    path,
                    () => Ask(Question(
                        Texts.Format(
                            "dashboard.merge.confirmSplit",
                            RowNames.Project(path).Title,
                            name),
                        [(Texts.Get("dashboard.merge.folder"), path)],
                        Texts.Get("dashboard.merge.splitExplain"),
                        Texts.Get("dashboard.merge.splitAction"),
                        () => Save(current => Without(current, path), flyout, changed),
                        Back))));
            }
        }

        var targets = others
            .Where(other => other != project)
            .OrderByDescending(other => RowNames.Project(other).Title == name)
            .Take(MostTargets)
            .ToList();
        list.Children.Add(Section(Texts.Format("dashboard.merge.into", name)));
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
                () => Ask(Question(
                    Texts.Format("dashboard.merge.confirmJoin", name, title),
                    [
                        (Texts.Get("dashboard.merge.folder"), project),
                        (Texts.Get("dashboard.merge.target"), target),
                    ],
                    Texts.Format("dashboard.merge.joinExplain", title),
                    Texts.Get("dashboard.merge.joinAction"),
                    () => Save(current => With(current, project, target), flyout, changed),
                    Back))));
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

    /// <summary>
    /// The confirmation the menu turns into: the question, each folder it touches with its
    /// full path, what will change, and buttons to go ahead or back to the list.
    /// </summary>
    private static StackPanel Question(
        string question,
        IReadOnlyList<(string Label, string Path)> folders,
        string explanation,
        string goAhead,
        Action confirm,
        Action back)
    {
        var panel = new StackPanel
        {
            Width = ListWidth,
            Padding = QuestionPadding,
            Spacing = QuestionSpacing,
        };
        panel.Children.Add(new TextBlock
        {
            Text = question,
            FontSize = QuestionSize,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        foreach (var (label, path) in folders)
        {
            panel.Children.Add(Folder(label, path));
        }

        panel.Children.Add(new TextBlock
        {
            Text = explanation,
            FontSize = ExplainSize,
            Foreground = DashboardCard.Brush("HakariInkMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
        });
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        var cancel = new Button
        {
            Content = Texts.Get("dashboard.merge.cancel"),
            Style = (Style)Application.Current.Resources["HakariButton"],
        };
        cancel.Click += (_, _) => back();
        var yes = new Button
        {
            Content = goAhead,
            Style = (Style)Application.Current.Resources["HakariPrimaryButton"],
        };
        yes.Click += (_, _) => confirm();
        buttons.Children.Add(cancel);
        buttons.Children.Add(yes);
        panel.Children.Add(buttons);
        return panel;
    }

    /// <summary>A folder in the question: what it is to the change, its name and path.</summary>
    private static Border Folder(string label, string path)
    {
        var (title, _) = RowNames.Project(path);
        var lines = new StackPanel();
        lines.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = SectionSize,
            Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
        });
        lines.Children.Add(TableCells.TwoLines(title, path));
        return new Border
        {
            Padding = FolderPadding,
            CornerRadius = new CornerRadius(6),
            Background = DashboardCard.Brush("HakariHoverBrush"),
            Child = lines,
        };
    }

    /// <summary>
    /// The monthly budget asked for in place: an amount, what it does, and save, remove (when
    /// one is set) or back. Enter saves; a blank or invalid amount is not taken.
    /// </summary>
    private static StackPanel BudgetQuestion(
        string name,
        decimal budget,
        string currency,
        Action<decimal?> save,
        Action back)
    {
        var panel = new StackPanel
        {
            Width = ListWidth,
            Padding = QuestionPadding,
            Spacing = QuestionSpacing,
        };
        panel.Children.Add(new TextBlock
        {
            Text = Texts.Format("dashboard.budget.question", name),
            FontSize = QuestionSize,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        var amount = new TextBox
        {
            Style = (Style)Application.Current.Resources["HakariTextBox"],
            MaxLength = BudgetLength,
            PlaceholderText = Texts.Format("dashboard.budget.placeholder", currency),
            Text = budget > 0 ? budget.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty,
        };
        panel.Children.Add(amount);
        panel.Children.Add(new TextBlock
        {
            Text = Texts.Get("dashboard.budget.explain"),
            FontSize = ExplainSize,
            Foreground = DashboardCard.Brush("HakariInkMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
        });

        void TrySave()
        {
            if (AmountText.Parse(amount.Text, out var valid) is { } value && valid)
            {
                save(value);
            }
        }

        amount.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                TrySave();
            }
        };
        amount.Loaded += (_, _) => amount.Focus(FocusState.Programmatic);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(Plain(Texts.Get("dashboard.merge.cancel"), back));
        if (budget > 0)
        {
            buttons.Children.Add(Plain(Texts.Get("dashboard.budget.remove"), () => save(null)));
        }

        var yes = new Button
        {
            Content = Texts.Get("dashboard.budget.save"),
            Style = (Style)Application.Current.Resources["HakariPrimaryButton"],
        };
        yes.Click += (_, _) => TrySave();
        buttons.Children.Add(yes);
        panel.Children.Add(buttons);
        return panel;
    }

    private static Button Plain(string text, Action click)
    {
        var button = new Button
        {
            Content = text,
            Style = (Style)Application.Current.Resources["HakariButton"],
        };
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>A budget saved, or removed with null.</summary>
    private static void SaveBudget(
        string project,
        decimal? amount,
        Microsoft.UI.Xaml.Controls.Flyout flyout,
        Action changed)
    {
        flyout.Hide();
        SettingsStore.Default.Update(current =>
        {
            var budgets = new Dictionary<string, decimal>(current.ProjectBudgets);
            if (amount is { } value)
            {
                budgets[project] = value;
            }
            else
            {
                budgets.Remove(project);
            }

            return current with { ProjectBudgets = budgets };
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
