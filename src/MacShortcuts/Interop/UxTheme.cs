using System.Runtime.InteropServices;

namespace MacShortcuts.Interop;

internal static unsafe partial class UxTheme
{
    public const int BP_CHECKBOX = 3;
    public const int CBS_UNCHECKEDNORMAL = 1;
    public const int CBS_UNCHECKEDHOT = 2;
    public const int CBS_UNCHECKEDPRESSED = 3;
    public const int CBS_UNCHECKEDDISABLED = 4;
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

    [LibraryImport("uxtheme.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    // Undocumented exports (by ordinal) that Explorer, Notepad and WinForms' dark mode also rely on.
    // They're how popup menus and scrollbars follow dark mode; missing ones are skipped.
    static readonly delegate* unmanaged<int, int> SetPreferredAppModeFn = (delegate* unmanaged<int, int>)Ordinal(135);
    static readonly delegate* unmanaged<void> FlushMenuThemesFn = (delegate* unmanaged<void>)Ordinal(136);
    static readonly delegate* unmanaged<IntPtr, int, int> AllowDarkModeForWindowFn = (delegate* unmanaged<IntPtr, int, int>)Ordinal(133);

    const int ForceDark = 2;
    const int ForceLight = 3;

    /// <summary>Makes popup menus created from now on dark or light.</summary>
    public static void SetAppDarkMode(bool dark)
    {
        if (SetPreferredAppModeFn != null) SetPreferredAppModeFn(dark ? ForceDark : ForceLight);
        if (FlushMenuThemesFn != null) FlushMenuThemesFn();
    }

    /// <summary>Lets a window's "DarkMode_*" theme classes also darken its scrollbars.</summary>
    public static void AllowDarkModeForWindow(IntPtr hwnd, bool allow)
    {
        if (AllowDarkModeForWindowFn != null) AllowDarkModeForWindowFn(hwnd, allow ? 1 : 0);
    }

    static void* Ordinal(int ordinal)
    {
        IntPtr module = Kernel32.LoadLibrary("uxtheme.dll");
        return module == IntPtr.Zero ? null : (void*)Kernel32.GetProcAddress(module, ordinal);
    }
}
