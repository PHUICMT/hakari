using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// The dashboard's Settings page: grouped cards, one row per setting. Every change is saved
/// at once; Hakari.exe watches the file and applies it, so there is no Save button.
/// </summary>
public sealed partial class SettingsPage : UserControl
{
    private const double RiseDistance = 16;

    private static readonly TimeSpan SectionStagger = TimeSpan.FromMilliseconds(45);

    private readonly SettingsStore store = SettingsStore.Default;

    /// <summary>True while controls are set from the file, so that is not saved back.</summary>
    private bool filling;

    public SettingsPage()
    {
        InitializeComponent();
        BuildCurrencyChoices();
        CurrencySuggestions.Attach(OtherCurrencyBox, _ => ApplyOtherCurrency());
        SupportLinks.Brand(SponsorsButton, SupportService.GitHubSponsors);
        SupportLinks.Brand(KoFiButton, SupportService.KoFi);
        SupportLinks.Brand(PromptPayButton, SupportService.PromptPay);
        PromptPayButton.Visibility = SupportLinks.ShowsPromptPay
            ? Visibility.Visible
            : Visibility.Collapsed;
        Fill();
        MakeSectionsFoldable();
    }

    public event EventHandler? LanguageChanged;

    /// <summary>The window this page is in, which pickers need as their owner.</summary>
    public IntPtr HostWindowHandle { get; set; }

    /// <summary>Sections rise in one after another; Reduced keeps only the fade.</summary>
    public void PlayEntrance()
    {
        var motion = SurfaceMotion.Current();
        var sections = Sections.Children.OfType<UIElement>().ToList();
        foreach (var section in sections)
        {
            section.Opacity = motion == AnimationSetting.Off ? 1 : 0;
            section.RenderTransform = new TranslateTransform();
        }

        // Without this the first choice gets keyboard focus and its focus frame.
        Scroller.Focus(FocusState.Programmatic);
        if (motion == AnimationSetting.Off)
        {
            return;
        }

        var storyboard = new Storyboard();
        for (var index = 0; index < sections.Count; index++)
        {
            AddSectionEntrance(storyboard, sections[index], index, motion);
        }

        storyboard.Begin();
    }

    private static void AddSectionEntrance(
        Storyboard storyboard,
        UIElement section,
        int index,
        AnimationSetting motion)
    {
        if (motion != AnimationSetting.Full)
        {
            storyboard.Children.Add(SurfaceMotion.Animate(
                section, "Opacity", 0, 1, SurfaceMotion.ReducedFade));
            return;
        }

        var delay = SectionStagger * index;
        var duration = SurfaceMotion.Entrance;
        storyboard.Children.Add(SurfaceMotion.Animate(
            section, "Opacity", 0, 1, duration, delay));
        storyboard.Children.Add(SurfaceMotion.Animate(
            section.RenderTransform, "Y", RiseDistance, 0, duration, delay));
    }

    private void Save(Func<HakariSettings, HakariSettings> change)
    {
        if (!filling)
        {
            store.Update(change);
        }
    }
}
