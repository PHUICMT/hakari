using Hakari.Core.Presentation.Widget;
using Hakari.Core.Settings;
using Microsoft.UI.Xaml;

namespace Hakari.Surfaces.Settings;

/// <summary>The taskbar layout card: ring, the two lines, and the live preview.</summary>
public sealed partial class SettingsWindow
{
    private static readonly (object Value, string Text)[] LineChoices =
    [
        (WidgetItem.Automatic, "Automatic"),
        (WidgetItem.CostToday, "Cost today"),
        (WidgetItem.CostThisMonth, "Cost this month"),
        (WidgetItem.BurnRate, "Burn rate"),
        (WidgetItem.SessionLimit, "5-hour limit"),
        (WidgetItem.WeeklyLimit, "Weekly limit"),
        (WidgetItem.MostPressingLimit, "Most pressing limit"),
        (WidgetItem.SecondAccount, "Second account"),
        (WidgetItem.Nothing, "Nothing"),
    ];

    private WidgetFacts? previewFacts;

    private void FillTaskbar(HakariSettings settings)
    {
        var layout = settings.Widget;
        RingOff.IsChecked = layout.Ring == WidgetRingSource.Off;
        RingSession.IsChecked = layout.Ring == WidgetRingSource.Session;
        RingWeekly.IsChecked = layout.Ring == WidgetRingSource.Weekly;
        RingPressing.IsChecked = layout.Ring == WidgetRingSource.MostPressing;

        TopSelect.SetChoices(LineChoices, layout.Top);
        BottomSelect.SetChoices(LineChoices, layout.Bottom);
        TopSelect.Selected -= OnTopSelected;
        TopSelect.Selected += OnTopSelected;
        BottomSelect.Selected -= OnBottomSelected;
        BottomSelect.Selected += OnBottomSelected;
        ShowPreview(layout);
    }

    private void OnRingChecked(object sender, RoutedEventArgs args)
    {
        var ring = ReferenceEquals(sender, RingOff) ? WidgetRingSource.Off
            : ReferenceEquals(sender, RingWeekly) ? WidgetRingSource.Weekly
            : ReferenceEquals(sender, RingPressing) ? WidgetRingSource.MostPressing
            : WidgetRingSource.Session;
        ChangeLayout(layout => layout with { Ring = ring });
    }

    private void OnTopSelected(object? sender, object value) =>
        ChangeLayout(layout => layout with { Top = (WidgetItem)value });

    private void OnBottomSelected(object? sender, object value) =>
        ChangeLayout(layout => layout with { Bottom = (WidgetItem)value });

    private void ChangeLayout(Func<WidgetLayout, WidgetLayout> change)
    {
        if (filling)
        {
            return;
        }

        var updated = store.Update(current => current with { Widget = change(current.Widget) });
        ShowPreview(updated.Widget);
    }

    /// <summary>Facts are read once; only the layout changes while this window is open.</summary>
    private void ShowPreview(WidgetLayout layout)
    {
        var now = DateTimeOffset.Now;
        previewFacts ??= WidgetFactsLoader.Load(now);
        Preview.Show(WidgetComposer.Compose(layout, previewFacts, now));
    }
}
