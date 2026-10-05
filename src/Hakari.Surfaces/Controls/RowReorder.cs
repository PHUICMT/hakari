using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// Lets the rows of a panel be dragged into a new order. The dragged row follows the
/// pointer, the others slide aside to show where it will land, and on release the new
/// order is reported. Presses on text fields and switches are left alone.
/// </summary>
internal sealed class RowReorder
{
    private const string OffsetPath = "Y";
    private const int DraggedLayer = 1;
    private const double DraggedOpacity = 0.92;

    /// <summary>Movement below this is a click, not a drag.</summary>
    private const double DragThreshold = 4;

    private readonly StackPanel panel;
    private readonly Action<IReadOnlyList<int>> reordered;
    private FrameworkElement? dragged;
    private int fromIndex;
    private int toIndex;
    private double startY;
    private bool moving;
    private List<double> rowTops = [];
    private readonly Dictionary<UIElement, Storyboard> slides = [];

    private RowReorder(StackPanel panel, Action<IReadOnlyList<int>> reordered)
    {
        this.panel = panel;
        this.reordered = reordered;
    }

    /// <param name="reordered">The rows' original indexes in their new order.</param>
    public static void Attach(StackPanel panel, Action<IReadOnlyList<int>> reordered)
    {
        var reorder = new RowReorder(panel, reordered);
        panel.PointerPressed += reorder.OnPressed;
        panel.PointerMoved += reorder.OnMoved;
        panel.PointerReleased += reorder.OnReleased;
        panel.PointerCaptureLost += (_, _) => reorder.Cancel();
    }

    private List<FrameworkElement> Rows => [.. panel.Children.OfType<FrameworkElement>()];

    private void OnPressed(object sender, PointerRoutedEventArgs args)
    {
        if (IsInsideControl(args.OriginalSource as DependencyObject)
            || RowAt(args.OriginalSource as DependencyObject) is not { } row)
        {
            return;
        }

        dragged = row;
        fromIndex = toIndex = Rows.IndexOf(row);
        startY = args.GetCurrentPoint(panel).Position.Y;
        rowTops = [.. Rows.Select(TopOf)];
        moving = false;
        panel.CapturePointer(args.Pointer);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs args)
    {
        if (dragged is null)
        {
            return;
        }

        var offset = args.GetCurrentPoint(panel).Position.Y - startY;
        if (!moving && Math.Abs(offset) < DragThreshold)
        {
            return;
        }

        if (!moving)
        {
            moving = true;
            Canvas.SetZIndex(dragged, DraggedLayer);
            dragged.Opacity = DraggedOpacity;
        }

        Offset(dragged).Y = offset;
        var center = rowTops[fromIndex] + offset + dragged.ActualHeight / 2;
        var target = TargetIndex(center);
        if (target != toIndex)
        {
            toIndex = target;
            MakeRoom();
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs args)
    {
        if (dragged is null)
        {
            return;
        }

        // Read the result before releasing: releasing raises "capture lost", which resets.
        var changed = moving && fromIndex != toIndex;
        var order = Enumerable.Range(0, Rows.Count).ToList();
        order.RemoveAt(fromIndex);
        order.Insert(toIndex, fromIndex);
        Reset();
        panel.ReleasePointerCapture(args.Pointer);
        if (changed)
        {
            reordered(order);
        }
    }

    private void Cancel()
    {
        if (dragged is not null)
        {
            Reset();
        }
    }

    /// <summary>Rows between the old and new place slide one row's height the other way.</summary>
    private void MakeRoom()
    {
        var height = dragged!.ActualHeight;
        var rows = Rows;
        for (var index = 0; index < rows.Count; index++)
        {
            if (index == fromIndex)
            {
                continue;
            }

            var shift = index > fromIndex && index <= toIndex ? -height
                : index < fromIndex && index >= toIndex ? height
                : 0;
            Slide(rows[index], shift);
        }
    }

    /// <summary>The new place: after each other row whose middle is above it.</summary>
    private int TargetIndex(double center) =>
        Enumerable.Range(0, rowTops.Count)
            .Count(index => index != fromIndex
                && rowTops[index] + Rows[index].ActualHeight / 2 < center);
    private void Slide(UIElement row, double to)
    {
        if (slides.Remove(row, out var running))
        {
            running.Stop();
        }

        var offset = Offset(row);
        if (SurfaceMotion.Current() == AnimationSetting.Off)
        {
            offset.Y = to;
            return;
        }

        var storyboard = new Storyboard();
        storyboard.Children.Add(
            SurfaceMotion.Animate(offset, OffsetPath, offset.Y, to, SurfaceMotion.Normal));
        storyboard.Completed += (_, _) =>
        {
            storyboard.Stop();
            offset.Y = to;
        };
        slides[row] = storyboard;
        storyboard.Begin();
    }

    /// <summary>Stops every slide first, so none can finish later and move a row again.</summary>
    private void Reset()
    {
        foreach (var slide in slides.Values)
        {
            slide.Stop();
        }

        slides.Clear();
        foreach (var row in Rows)
        {
            Offset(row).Y = 0;
            Canvas.SetZIndex(row, 0);
            row.Opacity = 1;
        }

        dragged = null;
        moving = false;
    }

    private double TopOf(FrameworkElement row) =>
        row.TransformToVisual(panel).TransformPoint(default).Y - Offset(row).Y;

    private static TranslateTransform Offset(UIElement row)
    {
        if (row.RenderTransform is TranslateTransform translate)
        {
            return translate;
        }

        translate = new TranslateTransform();
        row.RenderTransform = translate;
        return translate;
    }

    private FrameworkElement? RowAt(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is FrameworkElement row && ReferenceEquals(row.Parent, panel))
            {
                return row;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private static bool IsInsideControl(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is TextBox or ToggleButton or ButtonBase)
            {
                return true;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return false;
    }
}
