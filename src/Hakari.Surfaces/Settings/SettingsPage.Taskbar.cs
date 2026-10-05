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

    private static readonly (WidgetItem Value, string TextKey)[] LineChoiceKeys =
    [
        (WidgetItem.Automatic, "settings.item.automatic"),
        (WidgetItem.CostToday, "settings.item.costToday"),
        (WidgetItem.CostThisMonth, "settings.item.costMonth"),
        (WidgetItem.BurnRate, "settings.item.burnRate"),
        (WidgetItem.TokensToday, "settings.item.tokensToday"),
        (WidgetItem.TokensThisMonth, "settings.item.tokensMonth"),
        (WidgetItem.RepliesToday, "settings.item.repliesToday"),
        (WidgetItem.SessionLimit, "settings.item.session"),
        (WidgetItem.WeeklyLimit, "settings.item.weekly"),
        (WidgetItem.SessionAndWeeklyLimits, "settings.item.both"),
        (WidgetItem.MostPressingLimit, "settings.item.pressing"),
        (WidgetItem.SecondAccount, "settings.item.secondAccount"),
        (WidgetItem.Nothing, "settings.item.nothing"),
    ];

    private WidgetFacts? previewFacts;
    private string layoutTarget = SharedLayout;
    private DispatcherQueueTimer? turnTimer;

    /// <summary>
    /// What a block really shows for the saved choice: one account's block turns "second
    /// account" into its most pressing limit, so the list names that instead of nothing.
    /// </summary>
    private static WidgetItem Shown(WidgetItem item, MultiAccountMode mode) =>
        mode != MultiAccountMode.Together && item == WidgetItem.SecondAccount
            ? WidgetItem.MostPressingLimit
            : item;

    /// <summary>"Second account" only makes sense while all accounts share one block.</summary>
    private static IEnumerable<(object Value, string Text)> LineChoicesFor(MultiAccountMode mode) =>
        LineChoiceKeys
            .Where(choice => mode == MultiAccountMode.Together
                || choice.Value != WidgetItem.SecondAccount)
            .Select(choice => ((object)choice.Value, Texts.Get(choice.TextKey)));

    private void FillTaskbar(HakariSettings settings)
    {
        previewFacts ??= WidgetFactsLoader.Load(DateTimeOffset.Now);
        ModeTogether.IsChecked = settings.AccountsMode == MultiAccountMode.Together;
        ModeSideBySide.IsChecked = settings.AccountsMode == MultiAccountMode.SideBySide;
        ModeTakeTurns.IsChecked = settings.AccountsMode == MultiAccountMode.TakeTurns;

        LayoutTargetSelect.SetChoices(LayoutTargets(settings), layoutTarget);
        LayoutTargetSelect.Selected -= OnLayoutTargetSelected;
        LayoutTargetSelect.Selected += OnLayoutTargetSelected;
        TopSelect.Selected -= OnTopSelected;
        TopSelect.Selected += OnTopSelected;
        BottomSelect.Selected -= OnBottomSelected;
        BottomSelect.Selected += OnBottomSelected;

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
        LayoutTargetRow.Visibility = perAccount ? Visibility.Visible : Visibility.Collapsed;
        if (!perAccount)
        {
            layoutTarget = SharedLayout;
        }

        var layout = EditedLayout(settings);
        var wasFilling = filling;
        filling = true;
        RingOff.IsChecked = layout.Ring == WidgetRingSource.Off;
        RingSession.IsChecked = layout.Ring == WidgetRingSource.Session;
        RingWeekly.IsChecked = layout.Ring == WidgetRingSource.Weekly;
        RingPressing.IsChecked = layout.Ring == WidgetRingSource.MostPressing;
        RingBoth.IsChecked = layout.Ring == WidgetRingSource.SessionAndWeekly;
        var choices = LineChoicesFor(settings.AccountsMode).ToList();
        TopSelect.SetChoices(choices, Shown(layout.Top, settings.AccountsMode));
        BottomSelect.SetChoices(choices, Shown(layout.Bottom, settings.AccountsMode));
        filling = wasFilling;
    }

    private IEnumerable<(object Value, string Text)> LayoutTargets(HakariSettings settings) =>
        previewFacts!.Accounts
            .Select(account => ((object)account.AccountId, AccountLabels.Full(
                account.Account,
                settings.NicknameOf(account.AccountId))))
            .Prepend((SharedLayout, Texts.Get("settings.layoutFor.all")));

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
            LayoutTargetSelect.SetChoices(LayoutTargets(settings), layoutTarget);
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

    private void OnLayoutTargetSelected(object? sender, object value)
    {
        layoutTarget = (string)value;
        FillLayoutRows(store.Load());
    }

    private void OnRingChecked(object sender, RoutedEventArgs args)
    {
        var ring = ReferenceEquals(sender, RingOff) ? WidgetRingSource.Off
            : ReferenceEquals(sender, RingWeekly) ? WidgetRingSource.Weekly
            : ReferenceEquals(sender, RingPressing) ? WidgetRingSource.MostPressing
            : ReferenceEquals(sender, RingBoth) ? WidgetRingSource.SessionAndWeekly
            : WidgetRingSource.Session;
        ChangeLayout(layout => layout with { Ring = ring });
    }

    private void OnTopSelected(object? sender, object value) =>
        ChangeLayout(layout => layout with { Top = (WidgetItem)value });

    private void OnBottomSelected(object? sender, object value) =>
        ChangeLayout(layout => layout with { Bottom = (WidgetItem)value });

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

        while (PreviewRow.Children.Count > panels.Count)
        {
            PreviewRow.Children.RemoveAt(PreviewRow.Children.Count - 1);
        }

        for (var index = 0; index < panels.Count; index++)
        {
            if (index >= PreviewRow.Children.Count)
            {
                PreviewRow.Children.Add(new WidgetPreview());
            }

            ((WidgetPreview)PreviewRow.Children[index]).Show(panels[index]);
        }

        UpdateTurnTimer(settings, facts);
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
}
