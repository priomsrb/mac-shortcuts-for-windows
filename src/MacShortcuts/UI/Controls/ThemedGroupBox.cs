namespace MacShortcuts.UI.Controls;

/// <summary>
/// GroupBox whose frame is a subtle grey in dark mode; the default dark frame is near-white.
/// </summary>
internal sealed class ThemedGroupBox : GroupBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if (!Application.IsDarkModeEnabled)
        {
            base.OnPaint(e);
            return;
        }

        var g = e.Graphics;
        g.Clear(BackColor);
        var textSize = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        int textX = LogicalToDeviceUnits(8);
        int gap = LogicalToDeviceUnits(3);
        int frameTop = textSize.Height / 2;

        using (var pen = new Pen(Theming.DarkBorder))
        {
            int right = Width - 1, bottom = Height - 1;
            g.DrawLines(pen, [
                new Point(textX - gap, frameTop), new Point(0, frameTop), new Point(0, bottom),
                new Point(right, bottom), new Point(right, frameTop), new Point(textX + textSize.Width + gap, frameTop),
            ]);
        }
        TextRenderer.DrawText(g, Text, Font, new Point(textX, 0), ForeColor, TextFormatFlags.NoPadding);
    }
}
