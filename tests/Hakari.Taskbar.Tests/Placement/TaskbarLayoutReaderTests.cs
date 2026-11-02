using System.Drawing;
using Hakari.Taskbar.Placement;

namespace Hakari.Taskbar.Tests.Placement;

/// <summary>Positions read from a real taskbar on Windows 11, left-aligned and centered.</summary>
public class TaskbarLayoutReaderTests
{
    private const int Top = 1392;
    private const int Height = 48;
    private const string StartId = "StartButton";
    private const string ToggleButtonClass = "ToggleButton";
    private const string WidgetClass = "HakariTaskbarWidget";
    private const string FrameClass = "Taskbar.TaskbarFrameAutomationPeer";
    private const string TrayClass = "SystemTray.NormalButton";
    private const string AppButtonClass = "Taskbar.TaskListButtonAutomationPeer";
    private static readonly Rectangle TaskbarBounds = new(0, Top, 3440, Height);

    [Fact]
    public void A_centered_taskbar_is_free_from_its_left_edge_to_start()
    {
        var layout = Read(Start(left: 1513));

        Assert.Equal(Rectangle.FromLTRB(0, Top, 1513, Top + Height), layout.FreeBeforeStart);
    }

    [Fact]
    public void A_left_aligned_taskbar_has_nothing_free_before_start()
    {
        var layout = Read(Start(left: 0));

        Assert.Equal(0, layout.FreeBeforeStart.Width);
    }

    [Fact]
    public void Free_space_starts_after_a_button_already_there()
    {
        var widgetsButton = new TaskbarElement(
            ToggleButtonClass,
            "WidgetsButton",
            At(left: 0, width: 150));

        var layout = Read(Start(left: 1513), widgetsButton);

        Assert.Equal(150, layout.FreeBeforeStart.Left);
    }

    [Fact]
    public void Hakari_itself_does_not_take_the_free_space()
    {
        var ownWidget = new TaskbarElement(WidgetClass, string.Empty, At(left: 8, width: 495));

        var layout = Read(Start(left: 1513), ownWidget);

        Assert.Equal(0, layout.FreeBeforeStart.Left);
    }

    [Fact]
    public void What_reaches_across_start_does_not_take_the_free_space()
    {
        var frame = new TaskbarElement(FrameClass, "TaskbarFrame", TaskbarBounds);

        var layout = Read(Start(left: 1513), frame);

        Assert.Equal(0, layout.FreeBeforeStart.Left);
    }

    [Fact]
    public void Without_a_start_button_nothing_is_called_free()
    {
        var layout = Read();

        Assert.True(layout.FreeBeforeStart.IsEmpty);
    }

    private static TaskbarLayout Read(params TaskbarElement[] elements)
    {
        TaskbarElement[] common =
        [
            new(AppButtonClass, "Appid: MSEdge", At(left: 1664, width: 44)),
            new(TrayClass, "SystemTrayIcon", At(left: 3096, width: 32)),
        ];

        var layout = TaskbarLayoutReader.Summarize([.. elements, .. common], TaskbarBounds);
        Assert.NotNull(layout);
        return layout;
    }

    private static TaskbarElement Start(int left) =>
        new(ToggleButtonClass, StartId, At(left, width: 45));

    private static Rectangle At(int left, int width) => new(left, Top, width, Height);
}
