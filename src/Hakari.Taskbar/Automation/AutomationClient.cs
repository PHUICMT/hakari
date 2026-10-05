using System.Runtime.InteropServices;
using Interop.UIAutomationClient;

namespace Hakari.Taskbar.Automation;

/// <summary>One UI Automation COM client for the process, created on first use.</summary>
internal static class AutomationClient
{
    public const int BoundingRectanglePropertyId = 30001;
    public const int ClassNamePropertyId = 30012;

    private static readonly Lazy<IUIAutomation> LazyInstance = new(() => new CUIAutomation8());

    public static IUIAutomation Instance => LazyInstance.Value;

    public static bool IsAutomationProblem(Exception exception) =>
        exception is COMException
            or InvalidCastException
            or InvalidOperationException
            or ArgumentException;
}
