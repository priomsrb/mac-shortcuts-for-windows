using System.Runtime.InteropServices;

namespace MacShortcuts.Interop;

internal static partial class UxTheme
{
    public const int BP_CHECKBOX = 3;
    public const int CBS_UNCHECKEDNORMAL = 1;
    public const int CBS_CHECKEDNORMAL = 5;
    public const int TS_DRAW = 2;

    [LibraryImport("uxtheme.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr OpenThemeDataForDpi(IntPtr hwnd, string classList, int dpi);

    [LibraryImport("uxtheme.dll")]
    public static partial int CloseThemeData(IntPtr theme);

    [LibraryImport("uxtheme.dll")]
    public static partial int GetThemePartSize(IntPtr theme, IntPtr hdc, int part, int state, IntPtr rect, int size, out SIZE result);

    [LibraryImport("uxtheme.dll")]
    public static partial int DrawThemeBackground(IntPtr theme, IntPtr hdc, int part, int state, ref RECT rect, IntPtr clip);
}
