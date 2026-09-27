using System.Runtime.InteropServices;
using MacShortcuts.Interop;
using static MacShortcuts.Interop.ComCtl32;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.UxTheme;

namespace MacShortcuts.UI.Controls;

/// <summary>Draws checkbox glyphs from the Windows dark theme.</summary>
internal static class DarkCheckBoxes
{
    const string ThemeClass = "DarkMode_Explorer::Button";

    /// <summary>Overwrites images 0 (unchecked) and 1 (checked), the ListView state image layout.</summary>
    /// <returns>false if this version of Windows has no dark theme.</returns>
    public static bool Draw(IntPtr imageList, int dpi)
    {
        IntPtr theme = OpenThemeDataForDpi(IntPtr.Zero, ThemeClass, dpi);
        if (theme == IntPtr.Zero) return false;
        try
        {
            ImageList_GetIconSize(imageList, out int width, out int height);
            Replace(imageList, 0, theme, CBS_UNCHECKEDNORMAL, width, height);
            Replace(imageList, 1, theme, CBS_CHECKEDNORMAL, width, height);
            return true;
        }
        finally { _ = CloseThemeData(theme); }
    }

    /// <summary>Draws one glyph vertically centred at the left of <paramref name="bounds"/>.</summary>
    /// <returns>The glyph's width, or 0 if this version of Windows has no dark theme.</returns>
    public static int DrawGlyph(IntPtr hdc, RECT bounds, bool isChecked, bool hot, bool pressed, int dpi)
    {
        IntPtr theme = OpenThemeDataForDpi(IntPtr.Zero, ThemeClass, dpi);
        if (theme == IntPtr.Zero) return 0;
        try
        {
            // The checked states follow the four unchecked ones.
            int state = (isChecked ? CBS_CHECKEDNORMAL : CBS_UNCHECKEDNORMAL)
                + (pressed ? CBS_UNCHECKEDPRESSED - 1 : hot ? CBS_UNCHECKEDHOT - 1 : 0);
            GetThemePartSize(theme, hdc, BP_CHECKBOX, state, IntPtr.Zero, TS_DRAW, out var glyph);
            var rect = new RECT { left = bounds.left, top = bounds.top + (bounds.bottom - bounds.top - glyph.cy) / 2 };
            rect.right = rect.left + glyph.cx;
            rect.bottom = rect.top + glyph.cy;
            DrawThemeBackground(theme, hdc, BP_CHECKBOX, state, ref rect, IntPtr.Zero);
            return glyph.cx;
        }
        finally { _ = CloseThemeData(theme); }
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
}
