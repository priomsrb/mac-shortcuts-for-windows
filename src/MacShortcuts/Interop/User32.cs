using System.Runtime.InteropServices;

namespace MacShortcuts.Interop;

internal static partial class User32
{
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;

    public const int WM_QUIT = 0x0012;
    public const int WM_NOTIFY = 0x004E;
    public const int WM_SYSCOMMAND = 0x0112;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_THEMECHANGED = 0x031A;
    public const int SC_MINIMIZE = 0xF020;

    public const uint LLKHF_EXTENDED = 0x01;
    public const uint LLKHF_UP = 0x80;

    public const uint INPUT_MOUSE = 0;
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;

    public const int VK_LSHIFT = 0xA0;
    public const int VK_RSHIFT = 0xA1;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_LMENU = 0xA4;
    public const int VK_RMENU = 0xA5;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;

    /// <summary>Pass to <see cref="SetWindowsHookEx"/> via <see cref="Marshal.GetFunctionPointerForDelegate"/>, and keep the delegate alive.</summary>
    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    public static partial IntPtr SetWindowsHookEx(int idHook, IntPtr lpfn, IntPtr hMod, uint dwThreadId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(IntPtr hhk);

    [LibraryImport("user32.dll")]
    public static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    public static partial uint MapVirtualKey(uint uCode, uint uMapType);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int vKey);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    public static partial IntPtr WindowFromPoint(POINT point);

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    public static partial IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    public static partial int GetMessage(out MSG msg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostThreadMessage(uint idThread, int msg, IntPtr wParam, IntPtr lParam);
}
