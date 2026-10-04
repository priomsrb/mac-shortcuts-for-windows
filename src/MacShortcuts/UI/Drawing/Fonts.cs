using MacShortcuts.Interop;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI.Drawing;

/// <summary>The window's fonts at one DPI. Sizes are pixels at 96 DPI.</summary>
internal sealed unsafe class Fonts
{
    // Windows 11's variable Segoe UI where available, else Segoe UI.
    static readonly string TextFace = Resolve("Segoe UI Variable Text", "Segoe UI");
    static readonly string DisplayFace = Resolve("Segoe UI Variable Display", "Segoe UI");

    readonly List<IntPtr> _all = [];

    public Fonts(int dpi)
    {
        IntPtr Make(float px, int weight, string face)
        {
            IntPtr font = Create(-(int)MathF.Round(px * dpi / 96), weight, face);
            _all.Add(font);
            return font;
        }

        Body = Make(13, FW_NORMAL, TextFace);
        BodyStrong = Make(13, FW_SEMIBOLD, TextFace);
        Label = Make(14, FW_NORMAL, TextFace);
        Small = Make(12, FW_NORMAL, TextFace);
        SmallStrong = Make(12, FW_SEMIBOLD, TextFace);
        Caption = Make(11, FW_NORMAL, TextFace);
        CaptionStrong = Make(11, FW_SEMIBOLD, TextFace);
        Title = Make(22, FW_SEMIBOLD, DisplayFace);
        Heading = Make(18, FW_SEMIBOLD, DisplayFace);
        Subheading = Make(16, FW_SEMIBOLD, DisplayFace);
    }

    public IntPtr Body { get; }
    public IntPtr BodyStrong { get; }
    public IntPtr Label { get; }
    public IntPtr Small { get; }
    public IntPtr SmallStrong { get; }
    public IntPtr Caption { get; }
    public IntPtr CaptionStrong { get; }
    public IntPtr Title { get; }
    public IntPtr Heading { get; }
    public IntPtr Subheading { get; }

    /// <summary>Deletes the fonts, once the window using them is gone.</summary>
    public void Delete()
    {
        foreach (var font in _all) DeleteObject(font);
        _all.Clear();
    }

    static IntPtr Create(int height, int weight, string face)
    {
        const uint DEFAULT_CHARSET = 1, CLEARTYPE_QUALITY = 5;
        return CreateFont(height, 0, 0, 0, weight, 0, 0, 0, DEFAULT_CHARSET, 0, 0, CLEARTYPE_QUALITY, 0, face);
    }

    /// <summary>Returns <paramref name="face"/> if it's installed, else <paramref name="fallback"/>.</summary>
    static string Resolve(string face, string fallback)
    {
        IntPtr font = Create(-12, FW_NORMAL, face);
        IntPtr hdc = GetDC(IntPtr.Zero);
        IntPtr old = SelectObject(hdc, font);
        char* buffer = stackalloc char[64];
        int length = GetTextFace(hdc, 64, buffer);
        SelectObject(hdc, old);
        _ = ReleaseDC(IntPtr.Zero, hdc);
        DeleteObject(font);
        // The font mapper substitutes another face for one that isn't installed.
        return length > 0 && new string(buffer).Equals(face, StringComparison.OrdinalIgnoreCase) ? face : fallback;
    }
}
