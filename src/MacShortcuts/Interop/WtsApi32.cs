using System.Runtime.InteropServices;

namespace MacShortcuts.Interop;

/// <summary>Session notifications (lock, unlock, fast user switching).</summary>
internal static partial class WtsApi32
{
    public const uint NOTIFY_FOR_THIS_SESSION = 0;

    [LibraryImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WTSRegisterSessionNotification(IntPtr hwnd, uint flags);

    [LibraryImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WTSUnRegisterSessionNotification(IntPtr hwnd);
}
