using System.Text.RegularExpressions;
using MacShortcuts.Interop;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI.Drawing;

/// <summary>
/// Draws a shortcut such as "Alt+Shift+← / →" as a row of keycaps, like the website does.
/// Alt keys in the Mac-style column get the accent colour and a ⌘, since they act as Cmd.
/// </summary>
internal static partial class KeyCaps
{
    [GeneratedRegex(@"( / |, | … )")]
    private static partial Regex Separators();

    /// <summary>Splits "Alt+G / Alt+Shift+G" into keycap labels and separators (null label = separator).</summary>
    static List<(string Text, bool IsKey)> Parse(string shortcut)
    {
        var parts = new List<(string, bool)>();
        foreach (string piece in Separators().Split(shortcut))
        {
            if (piece is " / " or ", " or " … ")
            {
                parts.Add((piece.Trim(), false));
                continue;
            }
            // "Alt++" is Alt and the plus key.
            bool plusKey = piece.EndsWith("++");
            foreach (string key in (plusKey ? piece[..^2] : piece).Split('+'))
                if (key.Length > 0) parts.Add((key, true));
            if (plusKey) parts.Add(("+", true));
        }
        return parts;
    }

    /// <summary>
    /// Draws the keycaps inside <paramref name="cell"/>, vertically centred and clipped to it.
    /// </summary>
    /// <param name="cmd">Whether Alt keys are shown as ⌘ Cmd.</param>
    public static void Draw(IntPtr hdc, RECT cell, string shortcut, bool cmd, Palette palette, IntPtr font, IntPtr symbolFont, int dpi)
    {
        int Scale(int value) => value * dpi / 96;

        int capHeight = Scale(22), gap = Scale(4), padding = Scale(7), radius = Scale(5), lift = Math.Max(1, Scale(2));
        int top = cell.top + (cell.bottom - cell.top - capHeight) / 2;
        int x = cell.left;

        int saved = SaveDC(hdc);
        _ = IntersectClipRect(hdc, cell.left, cell.top, cell.right, cell.bottom);
        foreach (var (text, isKey) in Parse(shortcut))
        {
            if (!isKey)
            {
                int separatorWidth = Paint.Measure(text, font).cx;
                x += Scale(2);
                Paint.Text(hdc, text, font, palette.MutedText, Paint.Rect(x, top, separatorWidth, capHeight));
                x += separatorWidth + gap + Scale(2);
                continue;
            }

            bool isCmd = cmd && text == "Alt";
            int textWidth = Paint.Measure(text, font).cx;
            int symbolWidth = isCmd ? Paint.Measure("⌘", symbolFont).cx + Scale(4) : 0;
            int width = Math.Max(capHeight, textWidth + symbolWidth + 2 * padding);
            var bounds = Paint.Rect(x, top, width, capHeight);

            uint face = isCmd ? palette.KeyCmdFace : palette.KeyFace;
            uint edge = isCmd ? palette.KeyCmdEdge : palette.KeyEdge;
            uint ink = isCmd ? palette.Accent : palette.KeyText;
            Canvas.Render(hdc, bounds, null, c =>
            {
                c.FillRoundRect(0, lift, width, capHeight - lift, radius, edge);
                c.RoundRect(0, 0, width, capHeight - lift, radius, face, edge);
            });

            var face_ = Paint.Rect(x, top, width, capHeight - lift);
            var content = face_ with { left = x + padding, right = x + width - padding };
            if (isCmd)
            {
                Paint.Text(hdc, "⌘", symbolFont, ink, content, DT_SINGLELINE | DT_VCENTER | DT_LEFT);
                content.left += symbolWidth;
            }
            Paint.Text(hdc, text, font, ink, content,
                DT_SINGLELINE | DT_VCENTER | (isCmd ? DT_LEFT : DT_CENTER));
            x += width + gap;
            if (x > cell.right) break;
        }
        _ = RestoreDC(hdc, saved);
    }
}
