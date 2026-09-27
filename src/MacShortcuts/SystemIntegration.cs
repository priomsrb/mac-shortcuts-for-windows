using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace MacShortcuts;

internal static class StartWithWindows
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "MacShortcutsForWindows";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {Program.MinimizedArg}");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

internal static class Elevation
{
    public static bool IsAdmin { get; } =
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <returns>false if the user declined the UAC prompt.</returns>
    public static bool TryRestartElevated()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!, Program.WaitForPreviousArg)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}

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

internal static class Theming
{
    /// <summary>Readable replacement for the default dark-blue link/header text on dark backgrounds.</summary>
    public static Color DarkAccentText { get; } = Color.FromArgb(0x99, 0xEB, 0xFF);

    /// <summary>Subtle frame and divider colour on dark backgrounds.</summary>
    public static Color DarkBorder { get; } = Color.FromArgb(0x55, 0x55, 0x55);

    const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Whether Windows is set to dark mode for apps ("Choose your app mode").</summary>
    public static bool SystemIsDark
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
    }

    public static bool IsDark(AppTheme theme) => theme switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => SystemIsDark,
    };

    /// <summary>Applies to windows and menus created after this call.</summary>
    public static void Apply(AppTheme theme) =>
        Application.SetColorMode(IsDark(theme) ? SystemColorMode.Dark : SystemColorMode.Classic);
}
