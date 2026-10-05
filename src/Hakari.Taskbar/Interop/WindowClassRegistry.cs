using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

internal static class WindowClassRegistry
{
    /// <summary>Delegates must stay referenced for as long as Windows can call them.</summary>
    private static readonly Dictionary<string, WindowProcedure> RegisteredProcedures = [];

    public static void Register(string className, WindowProcedure procedure)
    {
        if (RegisteredProcedures.ContainsKey(className))
        {
            return;
        }

        var definition = new WindowClassDefinition
        {
            Size = (uint)Marshal.SizeOf<WindowClassDefinition>(),
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(procedure),
            Instance = Kernel32.GetModuleHandle(null),
            ClassName = className,
        };

        User32.RegisterClassEx(ref definition);
        RegisteredProcedures[className] = procedure;
    }
}
