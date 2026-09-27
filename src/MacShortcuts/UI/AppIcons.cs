namespace MacShortcuts.UI;

internal static class AppIcons
{
    public static Icon Enabled { get; } = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

    public static Icon Disabled { get; } = CreateDisabled(Enabled);

    static Icon CreateDisabled(Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        using var gray = new Bitmap(ToolStripRenderer.CreateDisabledImage(bitmap));
        return Icon.FromHandle(gray.GetHicon());
    }
}
