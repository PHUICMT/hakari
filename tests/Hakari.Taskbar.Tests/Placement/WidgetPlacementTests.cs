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

    private static TaskbarInfo Taskbar(int notificationLeft, int appButtonsRight) => new(
        Handle: IntPtr.Zero,
        IsPrimary: true,
        Bounds: new Rectangle(0, 1392, 3440, 48),
        NotificationArea: Rectangle.FromLTRB(notificationLeft, 1392, 3440, 1440),
        AppButtons: Rectangle.FromLTRB(161, 1392, appButtonsRight, 1440),
        Dpi: DefaultDpi,
        HasXamlLayout: true);
}
