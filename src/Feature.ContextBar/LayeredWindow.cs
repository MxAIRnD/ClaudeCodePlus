using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Feature.ContextBar;

/// <summary>
/// Per-pixel alpha for a layered window: hands a 32-bit ARGB bitmap to <c>UpdateLayeredWindow</c>,
/// which makes it the window's whole content and shape (transparent pixels are see-through).
/// </summary>
internal static class LayeredWindow
{
    private const uint UlwAlpha = 0x00000002;
    private const byte AcSrcOver = 0x00;
    private const byte AcSrcAlpha = 0x01;

    /// <summary>Shows <paramref name="bitmap"/> as the window's content, its top-left at <paramref name="location"/> (screen px).</summary>
    public static void Update(IntPtr window, Bitmap bitmap, Point location)
    {
        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr memory = CreateCompatibleDC(screen);
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr previous = IntPtr.Zero;
        try
        {
            // The one GetHbitmap overload that keeps the alpha channel of a 32bppArgb bitmap.
            hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            previous = SelectObject(memory, hBitmap);
            var size = new SIZE { cx = bitmap.Width, cy = bitmap.Height };
            var source = new POINT { x = 0, y = 0 };
            var destination = new POINT { x = location.X, y = location.Y };
            var blend = new BLENDFUNCTION { BlendOp = AcSrcOver, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AcSrcAlpha };
            _ = UpdateLayeredWindow(window, screen, ref destination, ref size, memory, ref source, 0, ref blend, UlwAlpha);
        }
        finally
        {
            if (previous != IntPtr.Zero)
            {
                _ = SelectObject(memory, previous);
            }

            if (hBitmap != IntPtr.Zero)
            {
                _ = DeleteObject(hBitmap);
            }

            _ = DeleteDC(memory);
            _ = ReleaseDC(IntPtr.Zero, screen);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);
}
