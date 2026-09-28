using MacShortcuts.Interop;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI.Drawing;

/// <summary>Plain GDI drawing: rectangles, lines and text.</summary>
internal static class Paint
{
    public static void Fill(IntPtr hdc, RECT rect, uint color)
    {
        IntPtr brush = CreateSolidBrush(color);
        FillRect(hdc, rect, brush);
        DeleteObject(brush);
    }

    public static void HorizontalLine(IntPtr hdc, int left, int right, int y, uint color) =>
        Fill(hdc, new RECT { left = left, top = y, right = right, bottom = y + 1 }, color);

    public static void VerticalLine(IntPtr hdc, int x, int top, int bottom, uint color) =>
        Fill(hdc, new RECT { left = x, top = top, right = x + 1, bottom = bottom }, color);

    /// <returns>The height of the drawn text.</returns>
    public static int Text(IntPtr hdc, string text, IntPtr font, uint color, RECT rect, uint format = DT_SINGLELINE | DT_VCENTER)
    {
        IntPtr old = SelectObject(hdc, font);
        _ = SetBkMode(hdc, TRANSPARENT);
        _ = SetTextColor(hdc, color);
        int height = DrawText(hdc, text, text.Length, ref rect, format | DT_NOPREFIX);
        SelectObject(hdc, old);
        return height;
    }

    public static SIZE Measure(string text, IntPtr font)
    {
        IntPtr hdc = GetDC(IntPtr.Zero);
        IntPtr old = SelectObject(hdc, font);
        GetTextExtentPoint32(hdc, text, text.Length, out var size);
        SelectObject(hdc, old);
        _ = ReleaseDC(IntPtr.Zero, hdc);
        return size;
    }

    /// <summary>The height of wrapped text in <paramref name="width"/> pixels.</summary>
    public static int MeasureWrapped(string text, IntPtr font, int width)
    {
        IntPtr hdc = GetDC(IntPtr.Zero);
        IntPtr old = SelectObject(hdc, font);
        var rect = new RECT { right = width };
        DrawText(hdc, text, text.Length, ref rect, DT_CALCRECT | DT_WORDBREAK | DT_NOPREFIX);
        SelectObject(hdc, old);
        _ = ReleaseDC(IntPtr.Zero, hdc);
        return rect.bottom;
    }

    /// <summary>
    /// A rounded panel with a 1-pixel outline. Only the corners are drawn smooth, which keeps large panels cheap.
    /// </summary>
    /// <param name="backdrop">The colour around the panel, which shows outside its rounded corners.</param>
    /// <param name="topFill">A different fill for a strip <paramref name="topHeight"/> pixels tall at the top.</param>
    public static void Panel(IntPtr hdc, RECT r, int radius, uint fill, uint border, uint backdrop, uint? topFill = null, int topHeight = 0)
    {
        int width = r.right - r.left, height = r.bottom - r.top;
        if (width < 2 * radius || height < 2 * radius) return;
        uint top = topFill ?? fill;
        Fill(hdc, r, fill);
        if (topHeight > 0) Fill(hdc, r with { bottom = r.top + topHeight }, top);
        HorizontalLine(hdc, r.left + radius, r.right - radius, r.top, border);
        HorizontalLine(hdc, r.left + radius, r.right - radius, r.bottom - 1, border);
        VerticalLine(hdc, r.left, r.top + radius, r.bottom - radius, border);
        VerticalLine(hdc, r.right - 1, r.top + radius, r.bottom - radius, border);

        foreach (var (x, y) in (ReadOnlySpan<(int, int)>)[(r.left, r.top), (r.right - radius, r.top), (r.left, r.bottom - radius), (r.right - radius, r.bottom - radius)])
        {
            // Each corner draws the whole panel, offset so that only its own corner lands in the square.
            float offsetX = r.left - x, offsetY = r.top - y;
            uint cornerFill = y == r.top && topHeight >= radius ? top : fill;
            Canvas.Render(hdc, Rect(x, y, radius, radius), backdrop,
                c => c.RoundRect(offsetX, offsetY, width, height, radius, cornerFill, border));
        }
    }

    public static RECT Rect(int x, int y, int width, int height) =>
        new() { left = x, top = y, right = x + width, bottom = y + height };

    public static RECT Inflate(RECT r, int amount) =>
        new() { left = r.left - amount, top = r.top - amount, right = r.right + amount, bottom = r.bottom + amount };

    public static bool Contains(RECT r, int x, int y) => x >= r.left && x < r.right && y >= r.top && y < r.bottom;
}
