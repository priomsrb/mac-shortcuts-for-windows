using MacShortcuts.Interop;
using MacShortcuts.Settings;
using Microsoft.Win32;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI;

/// <summary>The colours a window is painted with (COLORREFs).</summary>
internal readonly record struct Palette(bool IsDark, uint Window, uint Control, uint Text, uint GrayText, uint Accent, uint Border)
{
    public static Palette Light => new(false,
        Window: GetSysColor(COLOR_BTNFACE),
        Control: GetSysColor(COLOR_WINDOW),
        Text: GetSysColor(COLOR_WINDOWTEXT),
        GrayText: GetSysColor(COLOR_GRAYTEXT),
        Accent: GetSysColor(COLOR_WINDOWTEXT),
        Border: GetSysColor(COLOR_3DSHADOW));

    public static Palette Dark { get; } = new(true,
        Window: Rgb(0x20, 0x20, 0x20),
        Control: Rgb(0x19, 0x19, 0x19),
        Text: Rgb(0xF0, 0xF0, 0xF0),
        GrayText: Rgb(0x9A, 0x9A, 0x9A),
        // Readable replacement for the default dark-blue header text on dark backgrounds.
        Accent: Rgb(0x99, 0xEB, 0xFF),
        // Subtle frame and divider colour on dark backgrounds.
        Border: Rgb(0x55, 0x55, 0x55));

    public static Palette For(bool dark) => dark ? Dark : Light;
}

internal static class Theming
{
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

    /// <summary>Applies to menus created after this call; windows theme themselves when created.</summary>
    public static void Apply(AppTheme theme) => UxTheme.SetAppDarkMode(IsDark(theme));
}
