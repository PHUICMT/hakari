using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Settings;
using Hakari.Surfaces.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// The taskbar layout card: how several accounts share the widget, which layout is being
/// edited (everyone's or one account's), the ring and lines, and the live preview.
/// </summary>
public sealed partial class SettingsPage
{
    /// <summary>The "layout for" choice that edits the layout every account shares.</summary>
    private const string SharedLayout = "";

    private WidgetFacts? previewFacts;
    private LayoutEditor? layoutEditor;
    private string layoutTarget = SharedLayout;
    private DispatcherQueueTimer? turnTimer;


    private void FillTaskbar(HakariSettings settings)
    {
        previewFacts ??= WidgetFactsLoader.Load(DateTimeOffset.Now);
        EnsureLayoutEditor();
        ModeTogether.IsChecked = settings.AccountsMode == MultiAccountMode.Together;
        ModeSideBySide.IsChecked = settings.AccountsMode == MultiAccountMode.SideBySide;
        ModeTakeTurns.IsChecked = settings.AccountsMode == MultiAccountMode.TakeTurns;


        FillLayoutRows(settings);
        ShowPreview(settings);
    }

    /// <summary>Per-account layouts only matter when each account has a block of its own.</summary>
    private void FillLayoutRows(HakariSettings settings)
    {
        TurnSpeedRow.Visibility = settings.AccountsMode == MultiAccountMode.TakeTurns
            ? Visibility.Visible
            : Visibility.Collapsed;
        foreach (var choice in new[] { Precision0, Precision1, Precision2 })
        {
            var wasFillingPrecision = filling;
            filling = true;
            choice.IsChecked = choice.Tag as string
                == settings.PercentDecimals.ToString(CultureInfo.InvariantCulture);
            filling = wasFillingPrecision;
        }

        foreach (var choice in new[] { Turn4, Turn8, Turn15, Turn30 })
        {
            var wasFillingTurn = filling;
            filling = true;
            choice.IsChecked = choice.Tag as string
                == settings.TurnSeconds.ToString(CultureInfo.InvariantCulture);
            filling = wasFillingTurn;
        }

        var perAccount = settings.AccountsMode != MultiAccountMode.Together
            && previewFacts!.Accounts.Count > 1;
        if (!perAccount)
        {
            layoutTarget = SharedLayout;
        }

        layoutEditor?.ShowTargets(perAccount ? LayoutTargets(settings) : [], layoutTarget);
    }

    private List<LayoutTarget> LayoutTargets(HakariSettings settings) =>
    [
        new LayoutTarget(SharedLayout, Texts.Get("settings.layoutFor.all"), false),
        .. previewFacts!.Accounts.Select(account => new LayoutTarget(
            account.AccountId,
            AccountLabels.Short(account.Account, settings.NicknameOf(account.AccountId)),
            settings.AccountLayouts.ContainsKey(account.AccountId))),
    ];

    private WidgetLayout EditedLayout(HakariSettings settings) =>
        layoutTarget == SharedLayout ? settings.Widget : settings.LayoutOf(layoutTarget);

    private void OnAccountsModeChecked(object sender, RoutedEventArgs args)
    {
        if (filling)
        {
            return;
        }

        var mode = ReferenceEquals(sender, ModeTogether) ? MultiAccountMode.Together
            : ReferenceEquals(sender, ModeTakeTurns) ? MultiAccountMode.TakeTurns
            : MultiAccountMode.SideBySide;
        var updated = store.Update(current => current with { AccountsMode = mode });
        FillLayoutRows(updated);
        ShowPreview(updated);
    }

    private void FillTaskbarAfterAccountChange(HakariSettings settings)
    {
        previewFacts = WidgetFactsLoader.Load(DateTimeOffset.Now);
        filling = true;
        try
        {
            FillLayoutRows(settings);
        }
        finally
        {
            filling = false;
        }

        ShowPreview(settings);
    }

    /// <summary>A new precision needs fresh estimates, so the facts are read again.</summary>
    private void OnPrecisionChecked(object sender, RoutedEventArgs args)
    {
        if (filling || sender is not HakariSegment { Tag: string tag }
            || !int.TryParse(tag, CultureInfo.InvariantCulture, out var decimals))
        {
            return;
        }

        var updated = store.Update(current => current with { PercentDecimals = decimals });
        previewFacts = null;
        ShowPreview(updated);
    }

    private void OnTurnSpeedChecked(object sender, RoutedEventArgs args)
    {
        if (filling || sender is not HakariSegment { Tag: string tag }
            || !int.TryParse(tag, CultureInfo.InvariantCulture, out var seconds))
        {
            return;
        }

        var updated = store.Update(current => current with { TurnSeconds = seconds });
        ShowPreview(updated);
    }

    private void OnLayoutTargetChosen(object? sender, string target)
    {
        layoutTarget = target;
        var settings = store.Load();
        FillLayoutRows(settings);
        ShowPreview(settings);
    }

    /// <summary>The account drops its own layout and follows everyone's again.</summary>
    private void OnLayoutTargetReset(object? sender, string target)
    {
        var updated = store.Update(current => current with
        {
            AccountLayouts = current.AccountLayouts
                .Where(entry => entry.Key != target)
                .ToDictionary(entry => entry.Key, entry => entry.Value),
        });
        FillLayoutRows(updated);
        ShowPreview(updated);
    }

    /// <summary>Edits the shared layout, or gives the chosen account a layout of its own.</summary>
    private void ChangeLayout(Func<WidgetLayout, WidgetLayout> change)
    {
        if (filling)
        {
            return;
        }

        var target = layoutTarget;
        var updated = store.Update(current =>
        {
            if (target == SharedLayout)
            {
                return current with { Widget = change(current.Widget) };
            }

            var layouts = new Dictionary<string, WidgetLayout>(current.AccountLayouts)
            {
                [target] = change(current.LayoutOf(target)),
            };
            return current with { AccountLayouts = layouts };
        });
        layoutEditor?.ShowTargets(LayoutTargetsIfShown(updated), layoutTarget);
        ShowPreview(updated);
    }

    /// <summary>Facts are read once; only the layout changes while this window is open.</summary>
    private void ShowPreview(HakariSettings settings)
    {
        var now = DateTimeOffset.Now;
        previewFacts ??= WidgetFactsLoader.Load(now);
        var facts = previewFacts with { Nicknames = settings.AccountNicknames };
        var panels = WidgetPanels.Compose(
            settings.AccountsMode,
            settings.Widget,
            settings.LayoutOf,
            facts,
            now,
            settings.TurnLength);

        PreviewStrips.Show(panels);

        var layout = EditedLayout(settings);

        var (editorPanels, focused) = EditorPanels(settings, facts, panels, now);
        layoutEditor?.Refresh(layout, editorPanels, facts, settings.AccountsMode, focused);
        UpdateTurnTimer(settings, facts);
    }

    private void EnsureLayoutEditor()
    {
        if (layoutEditor is not null)
        {
            return;
        }

        layoutEditor = new LayoutEditor(
            () => EditedLayout(store.Load()),
            ChangeLayout,
            () => store.Load().SavedLayouts,
            change => ShowPreview(store.Update(current => current with
            {
                SavedLayouts = change(current.SavedLayouts),
            })));
        layoutEditor.TargetBar.Chosen += OnLayoutTargetChosen;
        layoutEditor.TargetBar.ResetRequested += OnLayoutTargetReset;
        LayoutEditorCard.Child = layoutEditor;
    }

    /// <summary>While taking turns the preview turns too, on the taskbar's clock.</summary>
    private void UpdateTurnTimer(HakariSettings settings, WidgetFacts facts)
    {
        var turning = settings.AccountsMode == MultiAccountMode.TakeTurns
            && facts.Accounts.Count > 1;
        if (!turning)
        {
            turnTimer?.Stop();
            return;
        }

        if (turnTimer is null)
        {
            turnTimer = DispatcherQueue.CreateTimer();
            turnTimer.Interval = settings.TurnLength;
            turnTimer.Tick += (_, _) => ShowPreview(store.Load());
            Unloaded += (_, _) => turnTimer.Stop();
        }

        turnTimer.Interval = settings.TurnLength;
        turnTimer.Start();
    }

    private List<LayoutTarget> LayoutTargetsIfShown(HakariSettings settings) =>
        settings.AccountsMode != MultiAccountMode.Together && previewFacts!.Accounts.Count > 1
            ? LayoutTargets(settings)
            : [];

    /// <summary>
    /// The editor's preview while one account is edited: side by side its block stays lit and
    /// the rest dim; taking turns it holds on that account instead of turning.
    /// </summary>
    private (IReadOnlyList<ComposedWidget> Panels, int Focused) EditorPanels(
        HakariSettings settings,
        WidgetFacts facts,
        IReadOnlyList<ComposedWidget> panels,
        DateTimeOffset now)
    {
        var index = facts.Accounts.ToList()
            .FindIndex(account => account.AccountId == layoutTarget);
        if (layoutTarget == SharedLayout || index < 0)
        {
            return (panels, -1);
        }

        if (settings.AccountsMode == MultiAccountMode.TakeTurns)
        {
            var account = facts.Accounts[index];
            return ([WidgetPanels.ForAccount(settings.LayoutOf(layoutTarget), facts, account, now)],
                -1);
        }

        return (panels, index < panels.Count ? index : -1);
    }
}