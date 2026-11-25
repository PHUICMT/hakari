using System.Globalization;
using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// A value picked by dragging, in Hakari's own style: a thin track that fills up to a round
/// thumb, with the value written beside it. It moves in whole steps, follows the keyboard
/// (arrows, Page Up and Down, Home, End), and tells a screen reader its range and value.
/// </summary>
/// <remarks>
/// <see cref="Changing"/> fires while dragging, for live text; <see cref="Changed"/> once the
/// value is let go, so a setting is saved once per drag, not on every pixel.
/// </remarks>
public sealed partial class HakariSlider : UserControl
{
    private const double TrackWidth = 180;
    private const double TrackHeight = 4;
    private const double HitHeight = 28;
    private const double ThumbSize = 16;
    private const double ThumbRing = 2;
    private const double LabelWidth = 44;
    private const double LabelGap = 12;
    private const double HoverScale = 1.15;
    private const double PressScale = 0.9;
    private const int PageSteps = 5;

    private readonly Border fill;
    private readonly Border thumb;
    private readonly ScaleTransform thumbScale = new();
    private readonly TranslateTransform thumbOffset = new();
    private readonly TextBlock label;
    private readonly Grid track;
    private readonly Grid root;
    private readonly ScaleTransform fillScale = new();
    private bool dragging;
    private bool hovered;
    private int value;

    public HakariSlider()
    {
        root = new Grid { ColumnSpacing = LabelGap };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TrackWidth) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelWidth) });
        IsTabStop = true;
        UseSystemFocusVisuals = true;

        // A see-through fill still takes pointer input across the whole hit area.
        track = new Grid
        {
            Height = HitHeight,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        };
        track.Children.Add(new Border
        {
            Height = TrackHeight,
            CornerRadius = new CornerRadius(TrackHeight / 2),
            Background = Brush("HakariLineStrongBrush"),
        });
        fill = new Border
        {
            Height = TrackHeight,
            CornerRadius = new CornerRadius(TrackHeight / 2),
            Background = Brush("HakariAccentBrush"),
            RenderTransform = fillScale,
        };
        track.Children.Add(fill);
        thumb = new Border
        {
            Width = ThumbSize,
            Height = ThumbSize,
            CornerRadius = new CornerRadius(ThumbSize / 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brush("HakariAccentBrush"),
            BorderBrush = Brush("HakariTileBrush"),
            BorderThickness = new Thickness(ThumbRing),
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
            RenderTransform = thumbScale,
        };
        var thumbHolder = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            RenderTransform = thumbOffset,
        };
        thumbHolder.Children.Add(thumb);
        track.Children.Add(thumbHolder);
        root.Children.Add(track);

        label = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = Brush("HakariInkBrush"),
        };
        Grid.SetColumn(label, 1);
        root.Children.Add(label);
        Content = root;

        track.PointerPressed += OnPressed;
        track.PointerMoved += OnMoved;
        track.PointerReleased += OnReleased;
        track.PointerCaptureLost += (_, _) => EndDrag();
        PointerEntered += (_, _) => Hover(true);
        PointerExited += (_, _) => Hover(false);
        KeyDown += OnKeyDown;
        SizeChanged += (_, _) => Place(animate: false);
        IsEnabledChanged += (_, _) => Opacity = IsEnabled ? 1 : DisabledOpacity;
        Show();
    }

    private const double DisabledOpacity = 0.45;

    /// <summary>The track's length; shorter where a row has little room.</summary>
    public double TrackLength
    {
        get => root.ColumnDefinitions[0].Width.Value;
        set => root.ColumnDefinitions[0].Width = new GridLength(value);
    }

    public int Minimum { get; set; }

    public int Maximum { get; set; } = 100;

    public int Step { get; set; } = 1;

    /// <summary>How the value is written beside the track; a percent unless set.</summary>
    public Func<int, string> Format { get; set; } =
        number => number.ToString(CultureInfo.CurrentCulture) + "%";

    /// <summary>The fill and thumb color, such as the warning color for a level.</summary>
    public Brush Accent
    {
        set
        {
            fill.Background = value;
            thumb.Background = value;
        }
    }

    public event EventHandler<int>? Changing;

    public event EventHandler<int>? Changed;

    public int Value
    {
        get => value;
        set
        {
            this.value = Snap(value);
            Show();
        }
    }

    internal string ValueText => Format(value);

    protected override AutomationPeer OnCreateAutomationPeer() => new SliderPeer(this);

    /// <summary>Moves by whole steps from the keyboard or a screen reader, and saves.</summary>
    internal void SetFromUser(int newValue)
    {
        var snapped = Snap(newValue);
        if (snapped == value)
        {
            return;
        }

        value = snapped;
        Show();
        Changing?.Invoke(this, value);
        Changed?.Invoke(this, value);
    }

    private int Snap(int raw)
    {
        var step = Math.Max(1, Step);
        var stepped = Minimum + (int)Math.Round((raw - Minimum) / (double)step) * step;
        return Math.Clamp(stepped, Minimum, Maximum);
    }

    private double Share() =>
        Maximum > Minimum ? (double)(value - Minimum) / (Maximum - Minimum) : 0;

    private void Show()
    {
        label.Text = Format(value);
        AutomationProperties.SetHelpText(this, label.Text);
        Place(animate: IsLoaded && !dragging);
    }

    /// <summary>A key or a click on the track glides there; a drag follows the pointer.</summary>
    private void Place(bool animate)
    {
        var width = track.ActualWidth > 0 ? track.ActualWidth : TrackWidth;
        var travel = Math.Max(0, width - ThumbSize);
        var offset = travel * Share();
        // The fill spans the track and is scaled from its left end, which animates cheaply.
        var fillShare = (offset + ThumbSize / 2) / width;
        if (animate && SurfaceMotion.Current() != AnimationSetting.Off)
        {
            SurfaceMotion.Settle(thumbOffset, "X", offset);
            SurfaceMotion.Settle(fillScale, "ScaleX", fillShare);
            return;
        }

        thumbOffset.X = offset;
        fillScale.ScaleX = fillShare;
    }

    private void OnPressed(object sender, PointerRoutedEventArgs args)
    {
        if (!IsEnabled)
        {
            return;
        }

        Focus(FocusState.Pointer);
        dragging = track.CapturePointer(args.Pointer);
        ScaleThumb(PressScale);
        MoveTo(args.GetCurrentPoint(track).Position.X, glide: true);
        args.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs args)
    {
        if (dragging)
        {
            MoveTo(args.GetCurrentPoint(track).Position.X, glide: false);
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs args)
    {
        track.ReleasePointerCapture(args.Pointer);
        EndDrag();
    }

    private int valueAtPress = int.MinValue;

    private void EndDrag()
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;
        ScaleThumb(hovered ? HoverScale : 1);
        if (value != valueAtPress)
        {
            Changed?.Invoke(this, value);
        }

        valueAtPress = int.MinValue;
    }

    private void MoveTo(double pointerX, bool glide)
    {
        if (valueAtPress == int.MinValue)
        {
            valueAtPress = value;
        }

        var travel = Math.Max(1, track.ActualWidth - ThumbSize);
        var share = Math.Clamp((pointerX - ThumbSize / 2) / travel, 0, 1);
        var next = Snap((int)Math.Round(Minimum + share * (Maximum - Minimum)));
        if (next == value)
        {
            return;
        }

        value = next;
        label.Text = Format(value);
        AutomationProperties.SetHelpText(this, label.Text);
        Place(animate: glide);
        Changing?.Invoke(this, value);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        var step = Math.Max(1, Step);
        int? next = args.Key switch
        {
            VirtualKey.Left or VirtualKey.Down => value - step,
            VirtualKey.Right or VirtualKey.Up => value + step,
            VirtualKey.PageDown => value - step * PageSteps,
            VirtualKey.PageUp => value + step * PageSteps,
            VirtualKey.Home => Minimum,
            VirtualKey.End => Maximum,
            _ => null,
        };
        if (next is { } target && IsEnabled)
        {
            SetFromUser(target);
            args.Handled = true;
        }
    }

    private void Hover(bool isOver)
    {
        hovered = isOver;
        if (!dragging && IsEnabled)
        {
            ScaleThumb(isOver ? HoverScale : 1);
        }
    }

    private void ScaleThumb(double to)
    {
        if (SurfaceMotion.Current() == AnimationSetting.Off)
        {
            thumbScale.ScaleX = to;
            thumbScale.ScaleY = to;
            return;
        }

        SurfaceMotion.Settle(thumbScale, "ScaleX", to);
        SurfaceMotion.Settle(thumbScale, "ScaleY", to);
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    /// <summary>Gives screen readers the range, the value and a way to set it.</summary>
    private sealed partial class SliderPeer(HakariSlider owner)
        : FrameworkElementAutomationPeer(owner), IRangeValueProvider
    {
        public double Minimum => owner.Minimum;

        public double Maximum => owner.Maximum;

        public double Value => owner.value;

        public double SmallChange => Math.Max(1, owner.Step);

        public double LargeChange => SmallChange * PageSteps;

        public bool IsReadOnly => !owner.IsEnabled;

        public void SetValue(double newValue) => owner.SetFromUser((int)Math.Round(newValue));

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.Slider;

        protected override object? GetPatternCore(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.RangeValue
                ? this
                : base.GetPatternCore(patternInterface);

        protected override string GetClassNameCore() => nameof(HakariSlider);
    }
}
