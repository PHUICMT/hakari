using System.Drawing;

namespace Hakari.Taskbar.Placement;

/// <summary>One thing UI Automation reports inside a taskbar.</summary>
public sealed record TaskbarElement(string ClassName, string AutomationId, Rectangle Bounds);
