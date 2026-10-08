namespace MacShortcuts.UI.Drawing;

internal enum Icon { Keyboard, Plus, Block, Wrench, Folder, Search }

/// <summary>The window's icons and control glyphs, drawn as strokes on a <see cref="Canvas"/>.</summary>
internal static class Glyphs
{
    /// <summary>Draws an outline icon, designed on a 16×16 grid, in a <paramref name="size"/>-pixel square.</summary>
    public static void Draw(Canvas c, Icon icon, float x, float y, float size, uint color)
    {
        float s = size / 16;
        float w = Math.Max(1.2f * s, 1.1f);
        (float, float) P(float px, float py) => (x + px * s, y + py * s);

        switch (icon)
        {
            case Icon.Keyboard:
                c.StrokeRoundRect(x + 1.5f * s, y + 4 * s, 13 * s, 8.5f * s, 1.5f * s, w, color);
                c.Lines(w, color, P(4, 7), P(5, 7));
                c.Lines(w, color, P(7.5f, 7), P(8.5f, 7));
                c.Lines(w, color, P(11, 7), P(12, 7));
                c.Lines(w, color, P(5, 10), P(11, 10));
                break;

            case Icon.Plus:
                c.StrokeRoundRect(x + 2 * s, y + 2 * s, 12 * s, 12 * s, 2.5f * s, w, color);
                c.Lines(w, color, P(8, 5), P(8, 11));
                c.Lines(w, color, P(5, 8), P(11, 8));
                break;

            case Icon.Block:
                c.StrokeEllipse(x + 2 * s, y + 2 * s, 12 * s, 12 * s, w, color);
                c.Lines(w, color, P(3.8f, 12.2f), P(12.2f, 3.8f));
                break;

            case Icon.Wrench:
                c.Lines(w, color, [.. Wrench().Select(p => P(p.X, p.Y))]);
                break;

            case Icon.Folder:
                c.Lines(w, color, P(1.5f, 4.5f), P(1.5f, 12.5f), P(14.5f, 12.5f), P(14.5f, 6), P(8, 6), P(6.5f, 4.5f), P(1.5f, 4.5f));
                break;

            case Icon.Search:
                c.StrokeEllipse(x + 2.5f * s, y + 2.5f * s, 9 * s, 9 * s, w, color);
                c.Lines(w, color, P(10.5f, 10.5f), P(14, 14));
                break;
        }
    }

    /// <summary>
    /// The outline of a wrench on the 16×16 grid: a round head at the top right with an open jaw,
    /// and a handle with a rounded end reaching to the bottom left.
    /// </summary>
    static List<(float X, float Y)> Wrench()
    {
        const float headX = 10.5f, headY = 5.5f, radius = 4f, handleLength = 9.5f, handleHalfWidth = 1.4f;
        const float jawHalfWidth = 1.4f, jawDepth = 1f;
        const int steps = 12;

        // Points are placed along the handle (a, towards the jaw) and across it (b).
        var points = new List<(float X, float Y)>();
        float k = MathF.Sqrt(0.5f);
        void Add(float a, float b) => points.Add((headX + (a + b) * k, headY + (b - a) * k));
        void Arc(float cx, float r, float from, float to)
        {
            for (int i = 0; i <= steps; i++)
            {
                float t = from + (to - from) * i / steps;
                Add(cx + r * MathF.Cos(t), r * MathF.Sin(t));
            }
        }

        float jaw = MathF.Asin(jawHalfWidth / radius), neck = MathF.Asin(handleHalfWidth / radius);
        Add(jawDepth, jawHalfWidth);
        Arc(0, radius, jaw, MathF.PI - neck);
        Arc(-handleLength, handleHalfWidth, MathF.PI / 2, 3 * MathF.PI / 2);
        Arc(0, radius, MathF.PI + neck, 2 * MathF.PI - jaw);
        Add(jawDepth, -jawHalfWidth);
        Add(jawDepth, jawHalfWidth);
        return points;
    }

    /// <summary>A <paramref name="size"/>-pixel checkbox.</summary>
    public static void CheckBox(Canvas c, float x, float y, float size, bool isChecked, Palette palette)
    {
        float s = size / 16;
        if (!isChecked)
        {
            c.RoundRect(x, y, size, size, 4 * s, palette.Surface, palette.CheckBorder, Math.Max(1, s));
            return;
        }
        c.FillRoundRect(x, y, size, size, 4 * s, palette.Accent);
        // The tick, designed on a 10×10 grid centred in the box.
        float ox = x + 3 * s, oy = y + 3 * s;
        c.Lines(1.8f * s, palette.OnAccent, (ox + 1.5f * s, oy + 5.2f * s), (ox + 3.8f * s, oy + 7.5f * s), (ox + 8.5f * s, oy + 2.5f * s));
    }

    /// <summary>A switch, <paramref name="height"/> tall and twice as wide.</summary>
    public static void Toggle(Canvas c, float x, float y, float height, bool on, Palette palette)
    {
        float width = height * 2, r = height / 2, knob = height * 0.6f, inset = (height - knob) / 2;
        if (on)
        {
            c.FillRoundRect(x, y, width, height, r, palette.Accent);
            c.FillEllipse(x + width - inset - knob, y + inset, knob, knob, palette.OnAccent);
        }
        else
        {
            c.RoundRect(x, y, width, height, r, palette.Surface, palette.CheckBorder, Math.Max(1, height / 20));
            c.FillEllipse(x + inset, y + inset, knob, knob, palette.MutedText);
        }
    }
}
