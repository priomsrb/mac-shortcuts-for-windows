using System.Globalization;

namespace MacShortcuts.Shortcuts;

/// <summary>Display names for virtual-key codes, in the style of the shortcut catalog ("Ctrl+←", "PgUp").</summary>
public static class KeyNames
{
    /// <summary>
    /// The name shown on a keycap. Never contains "+" except for the plus key itself, since keycaps are split on it.
    /// </summary>
    public static string Of(int vk) => vk switch
    {
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x70 and <= 0x87 => "F" + (vk - 0x6F).ToString(CultureInfo.InvariantCulture),
        >= 0x60 and <= 0x69 => "Num " + (vk - 0x60).ToString(CultureInfo.InvariantCulture),
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x13 => "Pause",
        0x14 => "Caps Lock",
        0x1B => "Esc",
        0x20 => "Space",
        0x21 => "PgUp",
        0x22 => "PgDn",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "←",
        0x26 => "↑",
        0x27 => "→",
        0x28 => "↓",
        0x2C => "PrtScn",
        0x2D => "Ins",
        0x2E => "Del",
        0x5D => "Menu",
        0x6A => "Num *",
        0x6B => "Num Plus",
        0x6D => "Num -",
        0x6E => "Num .",
        0x6F => "Num /",
        0x90 => "Num Lock",
        0x91 => "Scroll Lock",
        0xA6 => "Browser Back",
        0xA7 => "Browser Forward",
        0xBA => ";",
        0xBB => "+",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        _ => "Key " + vk.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>Modifiers then key, e.g. "Win+Ctrl+Shift+K".</summary>
    public static string Format(Mods mods, int vk)
    {
        string text = "";
        if (mods.HasFlag(Mods.Win)) text += "Win+";
        if (mods.HasFlag(Mods.Ctrl)) text += "Ctrl+";
        if (mods.HasFlag(Mods.Alt)) text += "Alt+";
        if (mods.HasFlag(Mods.Shift)) text += "Shift+";
        return text + Of(vk);
    }

    /// <summary>Whether <paramref name="vk"/> is Shift, Ctrl, Alt or a Windows key, which only modify other keys.</summary>
    public static bool IsModifier(int vk) => vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or (>= 0xA0 and <= 0xA5);
}
