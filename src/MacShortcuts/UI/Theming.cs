using MacShortcuts.Settings;
using Microsoft.Win32;

namespace MacShortcuts.UI;

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
