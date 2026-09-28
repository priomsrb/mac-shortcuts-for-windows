using MacShortcuts.Interop;
using MacShortcuts.Settings;
using Microsoft.Win32;

namespace MacShortcuts.UI;

/// <summary>The colours the settings window is painted with (COLORREFs).</summary>
internal sealed record Palette
{
    public required bool IsDark { get; init; }

    /// <summary>The page background.</summary>
    public required uint Window { get; init; }

    /// <summary>Panels, lists, text boxes and buttons.</summary>
    public required uint Surface { get; init; }

    /// <summary>A panel's header strip and a table's column headers.</summary>
    public required uint SurfaceAlt { get; init; }

    public required uint Text { get; init; }
    public required uint MutedText { get; init; }

    /// <summary>Panel outlines.</summary>
    public required uint Border { get; init; }

    /// <summary>Lines between rows.</summary>
    public required uint Divider { get; init; }

    /// <summary>Text box and button outlines.</summary>
    public required uint ControlBorder { get; init; }

    /// <summary>Buttons under the mouse.</summary>
    public required uint ControlHover { get; init; }

    public required uint Accent { get; init; }

    /// <summary>Text and glyphs drawn on <see cref="Accent"/>.</summary>
    public required uint OnAccent { get; init; }

    /// <summary>An unticked checkbox's outline.</summary>
    public required uint CheckBorder { get; init; }

    /// <summary>The selected category and list row.</summary>
    public required uint Selection { get; init; }
    public required uint SelectionText { get; init; }

    public required uint Rail { get; init; }
    public required uint RailBorder { get; init; }
    public required uint RailHover { get; init; }
    public required uint RailSelected { get; init; }
    public required uint RailSelectedBorder { get; init; }
    public required uint RailSelectedText { get; init; }

    /// <summary>Letter tiles and the tip box.</summary>
    public required uint Tile { get; init; }
    public required uint TileText { get; init; }

    public static Palette Light { get; } = new()
    {
        IsDark = false,
        Window = Hex(0xFAF9F7),
        Surface = Hex(0xFFFFFF),
        SurfaceAlt = Hex(0xF4F2EF),
        Text = Hex(0x22211F),
        MutedText = Hex(0x6D6A64),
        Border = Hex(0xE6E3DE),
        Divider = Hex(0xEEECE8),
        ControlBorder = Hex(0xDCD9D3),
        ControlHover = Hex(0xF4F2EF),
        Accent = Hex(0x2F5D50),
        OnAccent = Hex(0xFFFFFF),
        CheckBorder = Hex(0x8D8A84),
        Selection = Hex(0xE3EBE7),
        SelectionText = Hex(0x1F4439),
        Rail = Hex(0xEFECE7),
        RailBorder = Hex(0xE3DFD9),
        RailHover = Hex(0xE7E3DD),
        RailSelected = Hex(0xFFFFFF),
        RailSelectedBorder = Hex(0xE3DFD9),
        RailSelectedText = Hex(0x1F4439),
        Tile = Hex(0xEEEBE6),
        TileText = Hex(0x4B4944),
    };

    public static Palette Dark { get; } = new()
    {
        IsDark = true,
        Window = Hex(0x1C1B1A),
        Surface = Hex(0x262523),
        SurfaceAlt = Hex(0x2C2B29),
        Text = Hex(0xECEBE8),
        MutedText = Hex(0xA19D96),
        Border = Hex(0x3A3835),
        Divider = Hex(0x302E2C),
        ControlBorder = Hex(0x45423E),
        ControlHover = Hex(0x302E2C),
        Accent = Hex(0x7FB8A4),
        OnAccent = Hex(0x1C1B1A),
        CheckBorder = Hex(0x7A766F),
        Selection = Hex(0x2A3A34),
        SelectionText = Hex(0xA9D8C6),
        Rail = Hex(0x232220),
        RailBorder = Hex(0x34322F),
        RailHover = Hex(0x2C2A28),
        RailSelected = Hex(0x3B3834),
        RailSelectedBorder = Hex(0x4D4944),
        RailSelectedText = Hex(0xC8EBDD),
        Tile = Hex(0x34322F),
        TileText = Hex(0xC9C6C0),
    };

    public static Palette For(bool dark) => dark ? Dark : Light;

    /// <summary>A COLORREF from 0xRRGGBB.</summary>
    static uint Hex(uint rgb) => Gdi32.Rgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
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
