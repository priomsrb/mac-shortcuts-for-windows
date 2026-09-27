using System.Runtime.InteropServices;

namespace MacShortcuts.Interop;

internal static partial class DwmApi
{
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, in int value, int size);
}
