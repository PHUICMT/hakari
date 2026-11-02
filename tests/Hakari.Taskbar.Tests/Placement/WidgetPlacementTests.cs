using System.Drawing;
using Hakari.Taskbar.Placement;

namespace Hakari.Taskbar.Tests.Placement;

public class WidgetPlacementTests
{
    private const uint DefaultDpi = 96;
    private static readonly Size WidgetSize = new(130, 40);

    [Fact]
    public void Sits_just_left_of_the_notification_area_and_vertically_centered()
    {
        var taskbar = Taskbar(notificationLeft: 3136, appButtonsRight: 513);

        var placement = WidgetPlacement.LeftOfNotificationArea(taskbar, WidgetSize);

        Assert.Equal(new Rectangle(3002, 1396, 130, 40), placement);
    }

    [Fact]
    public void Steps_aside_when_app_buttons_reach_its_space()
    {
        var taskbar = Taskbar(notificationLeft: 3136, appButtonsRight: 3050);

        Assert.Null(WidgetPlacement.LeftOfNotificationArea(taskbar, WidgetSize));
    }

    [Fact]
    public void Waits_for_the_layout_of_a_secondary_taskbar()
    {
        var taskbar = Taskbar(notificationLeft: 3136, appButtonsRight: 513) with
        {
            IsPrimary = false,
            HasXamlLayout = false,
        };

        Assert.Null(WidgetPlacement.LeftOfNotificationArea(taskbar, WidgetSize));
    }

    [Fact]
    public void Scales_the_gap_with_dpi()
    {
        var taskbar = Taskbar(notificationLeft: 3136, appButtonsRight: 513) with { Dpi = 192 };

        var placement = WidgetPlacement.LeftOfNotificationArea(taskbar, WidgetSize);

        Assert.Equal(3136 - 8, placement?.Right);
    }

    [Fact]
    public void Prefers_the_tray_side_when_both_sides_fit()
    {
        var taskbar = Taskbar(notificationLeft: 3136, appButtonsRight: 1900) with
        {
            FreeBeforeStart = Rectangle.FromLTRB(0, 1392, 1500, 1440),
        };

        Assert.Equal(new Rectangle(3002, 1396, 130, 40), WidgetPlacement.Find(taskbar, WidgetSize));
    }

    [Fact]
    public void Goes_before_start_when_centered_buttons_fill_the_tray_side()
    {
        var taskbar = Taskbar(notificationLeft: 3136, appButtonsRight: 3050) with
        {
            FreeBeforeStart = Rectangle.FromLTRB(0, 1392, 500, 1440),
        };

        Assert.Equal(new Rectangle(8, 1396, 130, 40), WidgetPlacement.Find(taskbar, WidgetSize));
    }

    [Fact]
    public void Starts_after_what_is_already_before_start()
    {
        var taskbar = Taskbar(notificationLeft: 3136, appButtonsRight: 3050) with
        {
            FreeBeforeStart = Rectangle.FromLTRB(150, 1392, 500, 1440),
            Dpi = 192,
        };

        Assert.Equal(166, WidgetPlacement.Find(taskbar, WidgetSize)?.Left);
    }

    [Fact]
    public void Gives_up_when_neither_side_has_room()
    {
        var taskbar = Taskbar(notificationLeft: 3136, appButtonsRight: 3050) with
        {
            FreeBeforeStart = Rectangle.FromLTRB(0, 1392, 140, 1440),
        };

        Assert.Null(WidgetPlacement.Find(taskbar, WidgetSize));
    }

    [Fact]
    public void A_left_aligned_taskbar_has_no_room_before_start()
    {
        var taskbar = Taskbar(notificationLeft: 3136, appButtonsRight: 3050);

        Assert.Null(WidgetPlacement.Find(taskbar, WidgetSize));
    }

    private static TaskbarInfo Taskbar(int notificationLeft, int appButtonsRight) => new(
        Handle: IntPtr.Zero,
        IsPrimary: true,
        Bounds: new Rectangle(0, 1392, 3440, 48),
        NotificationArea: Rectangle.FromLTRB(notificationLeft, 1392, 3440, 1440),
        AppButtons: Rectangle.FromLTRB(161, 1392, appButtonsRight, 1440),
        Dpi: DefaultDpi,
        HasXamlLayout: true);
}
