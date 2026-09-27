using System.Runtime.InteropServices;

namespace MacShortcuts.Interop;

internal static partial class Kernel32
{
    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr GetModuleHandle(string? lpModuleName);

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr LoadLibrary(string fileName);

    /// <summary>Looks up an export by ordinal.</summary>
    [LibraryImport("kernel32.dll")]
    public static partial IntPtr GetProcAddress(IntPtr module, nint ordinal);
}
