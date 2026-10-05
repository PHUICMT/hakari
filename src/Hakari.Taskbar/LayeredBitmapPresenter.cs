using System.Drawing;
using Hakari.Taskbar.Interop;

namespace Hakari.Taskbar;

/// <summary>Pushes a per-pixel-alpha bitmap to a layered window.</summary>
internal static class LayeredBitmapPresenter
{
    private static readonly Color TransparentBackground = Color.FromArgb(0);

    public static bool Present(IntPtr windowHandle, Bitmap bitmap)
    {
        var screenContext = User32.GetDC(IntPtr.Zero);
        var memoryContext = Gdi32.CreateCompatibleDC(screenContext);
        var bitmapHandle = bitmap.GetHbitmap(TransparentBackground);
        var previousObject = Gdi32.SelectObject(memoryContext, bitmapHandle);

        try
        {
            var size = new NativeSize { Width = bitmap.Width, Height = bitmap.Height };
            var sourcePoint = new NativePoint();
            var blend = new BlendFunction
            {
                BlendOperation = LayeredWindowFlags.SourceOver,
                SourceConstantAlpha = LayeredWindowFlags.Opaque,
                AlphaFormat = LayeredWindowFlags.SourceAlpha,
            };

            return User32.UpdateLayeredWindow(
                windowHandle,
                screenContext,
                IntPtr.Zero,
                ref size,
                memoryContext,
                ref sourcePoint,
                0,
                ref blend,
                LayeredWindowFlags.Alpha);
        }
        finally
        {
            Gdi32.SelectObject(memoryContext, previousObject);
            Gdi32.DeleteObject(bitmapHandle);
            Gdi32.DeleteDC(memoryContext);
            User32.ReleaseDC(IntPtr.Zero, screenContext);
        }
    }
}
