using MacShortcuts.Interop;
using static MacShortcuts.Interop.Gdi32;

namespace MacShortcuts.UI.Drawing;

/// <summary>
/// Draws smooth shapes. GDI doesn't anti-alias, so shapes are drawn at <see cref="Factor"/> times the
/// size and scaled down with averaging. Coordinates are in device pixels relative to the rendered area.
/// </summary>
internal sealed unsafe class Canvas
{
    const int Factor = 4;

    readonly IntPtr _dc;

    Canvas(IntPtr dc) => _dc = dc;

    /// <summary>Draws onto <paramref name="bounds"/> of <paramref name="hdc"/>, which is first filled with <paramref name="background"/>.</summary>
    public static void Render(IntPtr hdc, RECT bounds, uint background, Action<Canvas> draw)
    {
        int width = bounds.right - bounds.left, height = bounds.bottom - bounds.top;
        if (width <= 0 || height <= 0) return;

        IntPtr dc = CreateCompatibleDC(hdc);
        IntPtr bitmap = CreateCompatibleBitmap(hdc, width * Factor, height * Factor);
        IntPtr oldBitmap = SelectObject(dc, bitmap);
        var canvas = new Canvas(dc);
        canvas.FillRect(0, 0, width, height, background);
        draw(canvas);

        int oldMode = SetStretchBltMode(hdc, HALFTONE);
        SetBrushOrgEx(hdc, 0, 0, IntPtr.Zero);
        StretchBlt(hdc, bounds.left, bounds.top, width, height, dc, 0, 0, width * Factor, height * Factor, SRCCOPY);
        _ = SetStretchBltMode(hdc, oldMode);

        SelectObject(dc, oldBitmap);
        DeleteObject(bitmap);
        DeleteDC(dc);
    }

    /// <summary>
    /// Draws onto a new transparent 32-bit bitmap (premultiplied alpha, as image lists and menus expect).
    /// The alpha comes from drawing once on black and once on white.
    /// </summary>
    public static IntPtr RenderBitmap(int width, int height, Action<Canvas> draw)
    {
        var header = new BITMAPINFOHEADER
        {
            biSize = sizeof(BITMAPINFOHEADER),
            biWidth = width,
            biHeight = -height,
            biPlanes = 1,
            biBitCount = 32,
        };
        IntPtr onBlack = CreateDIBSection(IntPtr.Zero, ref header, 0, out IntPtr blackBits, IntPtr.Zero, 0);
        IntPtr onWhite = CreateDIBSection(IntPtr.Zero, ref header, 0, out IntPtr whiteBits, IntPtr.Zero, 0);
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        var bounds = new RECT { right = width, bottom = height };

        IntPtr old = SelectObject(dc, onBlack);
        Render(dc, bounds, 0x000000, draw);
        SelectObject(dc, onWhite);
        Render(dc, bounds, 0xFFFFFF, draw);
        SelectObject(dc, old);
        DeleteDC(dc);
        GdiFlush();

        var black = (uint*)blackBits;
        var white = (uint*)whiteBits;
        for (int i = 0; i < width * height; i++)
        {
            // On white, a pixel is lighter by the background showing through: 255 - alpha.
            uint b = black[i], w = white[i];
            int through = Math.Max((int)((w >> 8) & 0xFF) - (int)((b >> 8) & 0xFF),
                Math.Max((int)(w & 0xFF) - (int)(b & 0xFF), (int)((w >> 16) & 0xFF) - (int)((b >> 16) & 0xFF)));
            uint alpha = (uint)Math.Clamp(255 - through, 0, 255);
            // Drawn on black, the colour is already premultiplied; rounding can leave it just above alpha.
            uint red = Math.Min((b >> 16) & 0xFF, alpha), green = Math.Min((b >> 8) & 0xFF, alpha), blue = Math.Min(b & 0xFF, alpha);
            black[i] = (alpha << 24) | (red << 16) | (green << 8) | blue;
        }
        DeleteObject(onWhite);
        return onBlack;
    }

    public void FillRect(float x, float y, float width, float height, uint color) =>
        WithFill(color, () => Gdi32.RoundRect(_dc, S(x), S(y), S(x + width) + 1, S(y + height) + 1, 0, 0));

    public void FillRoundRect(float x, float y, float width, float height, float radius, uint color) =>
        WithFill(color, () => Gdi32.RoundRect(_dc, S(x), S(y), S(x + width) + 1, S(y + height) + 1, S(2 * radius), S(2 * radius)));

    /// <summary>A rounded rectangle with an outline <paramref name="borderWidth"/> wide, drawn inside its bounds.</summary>
    public void RoundRect(float x, float y, float width, float height, float radius, uint fill, uint border, float borderWidth = 1)
    {
        FillRoundRect(x, y, width, height, radius, border);
        FillRoundRect(x + borderWidth, y + borderWidth, width - 2 * borderWidth, height - 2 * borderWidth,
            Math.Max(0, radius - borderWidth), fill);
    }

    public void FillEllipse(float x, float y, float width, float height, uint color) =>
        WithFill(color, () => Gdi32.Ellipse(_dc, S(x), S(y), S(x + width) + 1, S(y + height) + 1));

    public void StrokeEllipse(float x, float y, float width, float height, float lineWidth, uint color) =>
        WithStroke(color, lineWidth, () => Gdi32.Ellipse(_dc, S(x), S(y), S(x + width), S(y + height)));

    public void StrokeRoundRect(float x, float y, float width, float height, float radius, float lineWidth, uint color) =>
        WithStroke(color, lineWidth, () => Gdi32.RoundRect(_dc, S(x), S(y), S(x + width), S(y + height), S(2 * radius), S(2 * radius)));

    /// <summary>Connected line segments with round ends and joins.</summary>
    public void Lines(float lineWidth, uint color, params ReadOnlySpan<(float X, float Y)> points)
    {
        var scaled = new POINT[points.Length];
        for (int i = 0; i < points.Length; i++) scaled[i] = new POINT { X = S(points[i].X), Y = S(points[i].Y) };
        WithStroke(color, lineWidth, () =>
        {
            fixed (POINT* p = scaled) Polyline(_dc, p, scaled.Length);
        });
    }

    static int S(float value) => (int)MathF.Round(value * Factor);

    void WithFill(uint color, Action draw)
    {
        IntPtr brush = CreateSolidBrush(color);
        IntPtr oldBrush = SelectObject(_dc, brush);
        IntPtr oldPen = SelectObject(_dc, GetStockObject(NULL_PEN));
        draw();
        SelectObject(_dc, oldPen);
        SelectObject(_dc, oldBrush);
        DeleteObject(brush);
    }

    void WithStroke(uint color, float width, Action draw)
    {
        var brush = new LOGBRUSH { lbColor = color };
        IntPtr pen = ExtCreatePen(PS_GEOMETRIC | PS_SOLID | PS_ENDCAP_ROUND | PS_JOIN_ROUND, Math.Max(1, S(width)), brush, 0, IntPtr.Zero);
        IntPtr oldPen = SelectObject(_dc, pen);
        IntPtr oldBrush = SelectObject(_dc, GetStockObject(NULL_BRUSH));
        draw();
        SelectObject(_dc, oldBrush);
        SelectObject(_dc, oldPen);
        DeleteObject(pen);
    }
}
