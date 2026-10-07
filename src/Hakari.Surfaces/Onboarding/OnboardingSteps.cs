using System.Globalization;
using Hakari.Core.Accounts;
using Hakari.Core.Configuration;
using Hakari.Core.Indexing;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Settings;
using Hakari.Core.Sources;
using Hakari.Surfaces.Controls;
using Hakari.Surfaces.Motion;
using Hakari.Surfaces.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Onboarding;

/// <summary>What each first-run step shows, and what the last one saves.</summary>
internal sealed class OnboardingSteps(SettingsStore store)
{
    private const double RowSpacing = 10;
    private const double CheckSize = 18;
    private const double BarHeight = 4;
    private const double BarSegment = 0.3;
    private const string LogPattern = "*.jsonl";
    private static readonly TimeSpan SweepDuration = TimeSpan.FromMilliseconds(1400);
    private static readonly WidgetTemplate[] Looks =
        [WidgetTemplate.TwoLines, WidgetTemplate.Columns, WidgetTemplate.Minimal];

    private WidgetTemplate chosenLook = WidgetTemplate.TwoLines;
    private bool lookChanged;

    /// <summary>Step one: every place usage was found, with how many log files each has.</summary>
    public StackPanel Sources()
    {
        var settings = store.Load();
        var body = new StackPanel { Spacing = RowSpacing };
        var sources = SourceDiscovery.Discover(
            new SourceDiscoveryOptions(settings.WslMode, settings.ExtraConfigDirectories));
        foreach (var source in sources)
        {
            var status = Faint(Texts.Get("onboarding.counting"));
            body.Children.Add(CheckRow(SourceNames.Display(source.Id), status));
            _ = CountInto(source, status);
        }

        if (sources.Count == 0)
        {
            body.Children.Add(Faint(Texts.Get("onboarding.noSources")));
        }

        body.Children.Add(Sweep());
        body.Children.Add(Faint(Texts.Get("onboarding.reading")));
        body.Children.Add(Faint(Texts.Get("onboarding.moreSources")));
        return body;
    }

    /// <summary>Step two: a nickname for each account, and whether it shows at all.</summary>
    public StackPanel Accounts()
    {
        var settings = store.Load();
        var body = new StackPanel { Spacing = RowSpacing * 2 };
        var accounts = KnownAccounts();
        if (accounts.Count == 0)
        {
            body.Children.Add(Faint(Texts.Get("onboarding.noAccounts")));
            return body;
        }

        foreach (var account in accounts)
        {
            body.Children.Add(AccountBlock(account, settings));
        }

        body.Children.Add(Faint(Texts.Get("onboarding.tokenNote")));
        return body;
    }

    /// <summary>Step three: three starting looks, shown on a light and a dark taskbar.</summary>
    public StackPanel Look()
    {
        var body = new StackPanel { Spacing = RowSpacing * 2 };
        var strips = new TaskbarStrips();
        var choices = new HakariSegmented { HorizontalAlignment = HorizontalAlignment.Left };
        var facts = WidgetFactsLoader.Load(DateTimeOffset.Now);
        void Preview()
        {
            var settings = store.Load();
            var layout = WidgetTemplates.Apply(settings.Widget, chosenLook);
            strips.Show(WidgetPanels.Compose(
                settings.AccountsMode, layout, _ => layout, facts, DateTimeOffset.Now));
        }

        foreach (var look in Looks)
        {
            var segment = new HakariSegment
            {
                GroupName = "OnboardingLook",
                Content = Texts.Get($"settings.template.{look.ToString().ToLowerInvariant()}"),
                IsChecked = look == chosenLook,
            };
            segment.Checked += (_, _) =>
            {
                chosenLook = look;
                lookChanged = true;
                Preview();
            };
            choices.Children.Add(segment);
        }

        Preview();
        body.Children.Add(strips);
        body.Children.Add(choices);
        body.Children.Add(Faint(Texts.Get("onboarding.lookNote")));
        return body;
    }

    /// <summary>Finish keeps the look picked on step three; the rest saved as it changed.</summary>
    public void Finish()
    {
        var look = chosenLook;
        var keep = !lookChanged && look == WidgetTemplate.TwoLines;
        store.Update(current => current with
        {
            Widget = keep && !current.Widget.UsesSlots
                ? current.Widget
                : WidgetTemplates.Apply(current.Widget, look),
            OnboardingDone = true,
        });
    }

    private StackPanel AccountBlock(AccountInfo account, HakariSettings settings)
    {
        var details = new[] { account.Email, PlanNames.Short(account.Plan) }
            .Where(detail => !string.IsNullOrWhiteSpace(detail));
        var block = new StackPanel { Spacing = 6 };
        block.Children.Add(Faint(string.Join(" · ", details)));
        var name = new TextBox
        {
            Style = (Style)Application.Current.Resources["HakariTextBox"],
            Text = settings.NicknameOf(account.AccountId) ?? string.Empty,
            PlaceholderText = AccountLabels.Full(account),
            MaxLength = 24,
        };
        name.LostFocus += (_, _) => SaveNickname(account.AccountId, name.Text);
        block.Children.Add(name);

        // As in the design: whether to ask for this account's limits, on unless turned off.
        var limits = new HakariToggle
        {
            IsChecked = !settings.LimitsOffAccounts.Contains(account.AccountId),
        };
        store.Update(current =>
            current.WithLimitsChoice(account.AccountId, limits.IsChecked == true));
        limits.Click += (_, _) => store.Update(current =>
            current.WithLimitsChoice(account.AccountId, limits.IsChecked == true));
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(Text(Texts.Get("onboarding.showLimits")));
        Grid.SetColumn(limits, 1);
        row.Children.Add(limits);
        block.Children.Add(row);
        return block;
    }

    private void SaveNickname(string accountId, string text)
    {
        var name = text.Trim();
        store.Update(current =>
        {
            var names = new Dictionary<string, string>(current.AccountNicknames);
            if (name.Length == 0)
            {
                names.Remove(accountId);
            }
            else
            {
                names[accountId] = name;
            }

            return current with { AccountNicknames = names };
        });
    }


    /// <summary>The index may not exist yet on the very first run; then none are known.</summary>
    private static IReadOnlyList<AccountInfo> KnownAccounts()
    {
        try
        {
            using var index = IndexStore.OpenReadOnly(HakariPaths.DefaultIndexPath);
            return new AccountRepository(index).ListAccounts();
        }
        catch (Exception exception) when (exception is IOException
            or Microsoft.Data.Sqlite.SqliteException)
        {
            return [];
        }
    }

    /// <summary>Counted off the UI thread; a missing folder means no usage there.</summary>
    private static async Task CountInto(UsageSource source, TextBlock status)
    {
        var count = await Task.Run(() =>
        {
            try
            {
                return Directory.Exists(source.ProjectsDirectory)
                    ? Directory.EnumerateFiles(
                        source.ProjectsDirectory, LogPattern, SearchOption.AllDirectories).Count()
                    : 0;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException)
            {
                return 0;
            }
        });
        status.Text = count == 0
            ? Texts.Get("onboarding.noUsage")
            : Texts.Format("onboarding.files", count.ToString("N0", CultureInfo.InvariantCulture));
    }

    private static Grid CheckRow(string name, TextBlock status)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new Border
        {
            Width = CheckSize,
            Height = CheckSize,
            CornerRadius = new CornerRadius(4),
            Background = Brush("HakariAccentBrush"),
            Child = new FontIcon
            {
                Glyph = "",
                FontSize = 11,
                FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
                Foreground = Brush("HakariOnAccentBrush"),
            },
        });
        var label = Text(name);
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        status.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(status, 2);
        row.Children.Add(status);
        return row;
    }

    /// <summary>
    /// An indeterminate bar: a short piece sweeps across and starts again, so it reads as
    /// working without claiming how far. Off keeps the piece still.
    /// </summary>
    private static Grid Sweep()
    {
        var track = new Grid
        {
            Height = BarHeight,
            CornerRadius = new CornerRadius(BarHeight / 2),
            Background = Brush("HakariLineBrush"),
            Margin = new Thickness(0, 8, 0, 0),
        };
        var piece = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(BarHeight / 2),
            Background = Brush("HakariAccentBrush"),
        };
        var offset = new TranslateTransform();
        piece.RenderTransform = offset;
        track.Children.Add(piece);
        var storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        track.Unloaded += (_, _) => storyboard.Stop();
        track.SizeChanged += (_, args) =>
        {
            var width = args.NewSize.Width;
            piece.Width = width * BarSegment;
            storyboard.Stop();
            storyboard.Children.Clear();
            if (SurfaceMotion.Current() == AnimationSetting.Off)
            {
                return;
            }

            var sweep = new DoubleAnimation
            {
                From = -piece.Width,
                To = width,
                Duration = SweepDuration,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Storyboard.SetTarget(sweep, offset);
            Storyboard.SetTargetProperty(sweep, "X");
            storyboard.Children.Add(sweep);
            storyboard.Begin();
        };
        return track;
    }

    private static TextBlock Text(string text) => new()
    {
        Text = text,
        FontSize = 14,
        Foreground = Brush("HakariInkBrush"),
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private static TextBlock Faint(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = Brush("HakariInkFaintBrush"),
        TextWrapping = TextWrapping.Wrap,
    };

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
