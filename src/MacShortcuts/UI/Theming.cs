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

    /// <summary>Keycaps: the face, the edge below it and the text; and the same for Cmd (Alt) keys.</summary>
    public required uint KeyFace { get; init; }
    public required uint KeyEdge { get; init; }
    public required uint KeyText { get; init; }
    public required uint KeyCmdFace { get; init; }
    public required uint KeyCmdEdge { get; init; }
    public required uint KeyCmdText { get; init; }

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
        KeyFace = Hex(0xF3F1ED),
        KeyEdge = Hex(0xBDB9B1),
        KeyText = Hex(0x22211F),
        KeyCmdFace = Hex(0xDDEAE5),
        KeyCmdEdge = Hex(0x93B5A9),
        KeyCmdText = Hex(0x2F5D50),
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
        KeyFace = Hex(0x3A3835),
        KeyEdge = Hex(0x1A1918),
        KeyText = Hex(0xECEBE8),
        KeyCmdFace = Hex(0x35504A),
        KeyCmdEdge = Hex(0x1A2925),
        KeyCmdText = Hex(0x7FB8A4),
    };

    /// <summary>The base palette with every accent-derived colour taken from the Windows accent colour.</summary>
    public static Palette For(bool dark)
    {
        uint accent = Theming.SystemAccent;
        const uint white = 0xFFFFFF;
        if (dark)
        {
            // Keep the accent bright enough to read on the dark surfaces.
            uint a = Luminance(accent) < 0.35 ? Mix(accent, white, 0.35) : accent;
            return Dark with
            {
                Accent = Hex(a),
                OnAccent = Hex(Luminance(a) > 0.5 ? 0x1C1B1A : white),
                Selection = Hex(Mix(a, 0x262523, 0.18)),
                SelectionText = Hex(Mix(a, white, 0.45)),
                RailSelectedText = Hex(Mix(a, white, 0.65)),
                KeyCmdFace = Hex(Mix(a, 0x3A3835, 0.12)),
                KeyCmdEdge = Hex(Mix(a, 0x1A1918, 0.2)),
                KeyCmdText = Hex(Mix(a, 0xECEBE8, 0.45)),
            };
        }
        // Keep the accent dark enough for white text on top of it.
        uint l = Luminance(accent) > 0.4 ? Mix(accent, 0x000000, 0.35) : accent;
        return Light with
        {
            Accent = Hex(l),
            Selection = Hex(Mix(l, white, 0.12)),
            SelectionText = Hex(Mix(l, 0x000000, 0.4)),
            RailSelectedText = Hex(Mix(l, 0x000000, 0.4)),
            KeyCmdFace = Hex(Mix(l, 0xF3F1ED, 0.08)),
            KeyCmdEdge = Hex(Mix(l, 0xBDB9B1, 0.25)),
            KeyCmdText = Hex(Mix(l, 0x22211F, 0.65)),
        };
    }

    /// <summary>Blends 0xRRGGBB <paramref name="from"/> towards <paramref name="to"/>; <paramref name="amount"/> is how much of <paramref name="from"/> remains.</summary>
    static uint Mix(uint from, uint to, double amount)
    {
        uint Channel(int shift) => (uint)Math.Round(((from >> shift) & 0xFF) * amount + ((to >> shift) & 0xFF) * (1 - amount));
        return Channel(16) << 16 | Channel(8) << 8 | Channel(0);
    }

    /// <summary>Approximate perceived brightness, 0 to 1.</summary>
    static double Luminance(uint rgb) =>
        (0.2126 * ((rgb >> 16) & 0xFF) + 0.7152 * ((rgb >> 8) & 0xFF) + 0.0722 * (rgb & 0xFF)) / 255;

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

    /// <summary>The Windows accent colour as 0xRRGGBB, falling back to the previous teal.</summary>
    public static uint SystemAccent
    {
        get
        {
            // The colour picked in Settings > Personalization > Colors, stored as 0xAABBGGRR.
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            if (key?.GetValue("AccentColorMenu") is int abgr)
            {
                uint v = (uint)abgr;
                return (v & 0xFF) << 16 | (v & 0xFF00) | (v >> 16) & 0xFF;
            }
            return 0x2F5D50;
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
