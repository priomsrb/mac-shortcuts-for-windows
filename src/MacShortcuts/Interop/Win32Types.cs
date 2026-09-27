using System.Runtime.InteropServices;

namespace MacShortcuts.Interop;

// Geometry structs shared across Win32 APIs.

[StructLayout(LayoutKind.Sequential)]
internal struct POINT { public int X, Y; }

[StructLayout(LayoutKind.Sequential)]
internal struct RECT { public int left, top, right, bottom; }

[StructLayout(LayoutKind.Sequential)]
internal struct SIZE { public int cx, cy; }
