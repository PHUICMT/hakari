using System.Runtime.InteropServices;
using Interop.UIAutomationClient;

namespace Hakari.Taskbar.Automation;

/// <summary>Forwards UI Automation structure-changed events to a callback.</summary>
[ComVisible(true)]
internal sealed class StructureChangedHandler(Action onChanged)
    : IUIAutomationStructureChangedEventHandler
{
    public void HandleStructureChangedEvent(
        IUIAutomationElement sender,
        StructureChangeType changeType,
        int[] runtimeId) => onChanged();
}
