using System.Runtime.InteropServices;

namespace MacShortcuts.UI.Controls;

/// <summary>Draws checkbox glyphs from the Windows dark theme into a native image list.</summary>
internal static class DarkCheckBoxes
{
    const int BP_CHECKBOX = 3;
    const int CBS_UNCHECKEDNORMAL = 1;
    const int CBS_CHECKEDNORMAL = 5;
    const int TS_DRAW = 2;

    /// <summary>Overwrites images 0 (unchecked) and 1 (checked), the ListView state image layout.</summary>
    /// <returns>false if this version of Windows has no dark theme.</returns>
    public static bool Draw(IntPtr imageList, int dpi)
    {
        IntPtr theme = OpenThemeDataForDpi(IntPtr.Zero, "DarkMode_Explorer::Button", dpi);
        if (theme == IntPtr.Zero) return false;
        try
        {
            ImageList_GetIconSize(imageList, out int width, out int height);
            Replace(imageList, 0, theme, CBS_UNCHECKEDNORMAL, width, height);
            Replace(imageList, 1, theme, CBS_CHECKEDNORMAL, width, height);
            return true;
        }
        finally { CloseThemeData(theme); }
    }

    static void Replace(IntPtr imageList, int index, IntPtr theme, int state, int width, int height)
    {
        // Draw into a zeroed 32-bit DIB so the theme's anti-aliased edges keep their alpha.
        var header = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height,
            biPlanes = 1,
            biBitCount = 32,
        };
        IntPtr dib = CreateDIBSection(IntPtr.Zero, ref header, 0, out _, IntPtr.Zero, 0);
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        IntPtr previous = SelectObject(dc, dib);
        try
        {
            GetThemePartSize(theme, IntPtr.Zero, BP_CHECKBOX, state, IntPtr.Zero, TS_DRAW, out var glyph);
            int w = Math.Min(glyph.cx, width), h = Math.Min(glyph.cy, height);
            var rect = new RECT { left = (width - w) / 2, top = (height - h) / 2 };
            rect.right = rect.left + w;
            rect.bottom = rect.top + h;
            DrawThemeBackground(theme, dc, BP_CHECKBOX, state, ref rect, IntPtr.Zero);
            GdiFlush();
        }
        finally
        {
            SelectObject(dc, previous);
            DeleteDC(dc);
        }
        ImageList_Replace(imageList, index, dib, IntPtr.Zero);
        DeleteObject(dib);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr OpenThemeDataForDpi(IntPtr hwnd, string classList, int dpi);

    [DllImport("uxtheme.dll")]
    static extern int CloseThemeData(IntPtr theme);

    [DllImport("uxtheme.dll")]
    static extern int GetThemePartSize(IntPtr theme, IntPtr hdc, int part, int state, IntPtr rect, int size, out SIZE result);

    [DllImport("uxtheme.dll")]
    static extern int DrawThemeBackground(IntPtr theme, IntPtr hdc, int part, int state, ref RECT rect, IntPtr clip);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER header, uint usage, out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    static extern bool GdiFlush();

    [DllImport("comctl32.dll")]
    static extern bool ImageList_GetIconSize(IntPtr imageList, out int cx, out int cy);

    [DllImport("comctl32.dll")]
    static extern bool ImageList_Replace(IntPtr imageList, int index, IntPtr image, IntPtr mask);
}
