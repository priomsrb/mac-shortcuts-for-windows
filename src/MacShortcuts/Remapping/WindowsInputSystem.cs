using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MacShortcuts.Interop;
using MacShortcuts.Shortcuts;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.Remapping;

internal sealed class WindowsInputSystem : IInputSystem
{
    /// <summary>Tags input we inject so our own hooks can ignore it.</summary>
    public static readonly IntPtr Signature = new(0x4D414353); // "MACS"

    static readonly int InputSize = Marshal.SizeOf<INPUT>();

    readonly ConcurrentDictionary<uint, string> _processNames = new();

    public bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    public string GetForegroundProcessName() => GetProcessName(GetForegroundWindow());

    public string GetProcessNameAt(POINT point) => GetProcessName(WindowFromPoint(point));

    public void MinimizeForegroundWindow() =>
        PostMessage(GetForegroundWindow(), WM_SYSCOMMAND, SC_MINIMIZE, IntPtr.Zero);

    public void Send(IReadOnlyList<SyntheticInput> inputs)
    {
        if (inputs.Count == 0) return;
        var native = new INPUT[inputs.Count];
        for (int i = 0; i < native.Length; i++) native[i] = ToNative(inputs[i]);
        SendInput((uint)native.Length, native, InputSize);
    }

    /// <summary>Forget cached process names, in case a process ID has since been reused.</summary>
    public void ClearProcessNames() => _processNames.Clear();

    string GetProcessName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "";
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (_processNames.TryGetValue(pid, out var name)) return name;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            name = p.ProcessName.ToLowerInvariant();
        }
        catch
        {
            name = "";
        }
        if (_processNames.Count > 256) _processNames.Clear();
        _processNames[pid] = name;
        return name;
    }

    static INPUT ToNative(SyntheticInput input) => input switch
    {
        SyntheticInput.Key k => KeyInput((ushort)k.Vk, (ushort)MapVirtualKey((uint)k.Vk, 0),
            (k.Up ? KEYEVENTF_KEYUP : 0) | (IsExtendedKey(k.Vk) ? KEYEVENTF_EXTENDEDKEY : 0)),
        SyntheticInput.Replay r => KeyInput((ushort)r.Original.Vk, (ushort)r.Original.ScanCode,
            (r.Original.Up ? KEYEVENTF_KEYUP : 0) | (r.Original.Extended ? KEYEVENTF_EXTENDEDKEY : 0)),
        SyntheticInput.LeftButton b => new INPUT
        {
            type = INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MOUSEINPUT { dwFlags = b.Up ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_LEFTDOWN, dwExtraInfo = Signature },
            },
        },
        _ => throw new UnreachableException(),
    };

    static INPUT KeyInput(ushort vk, ushort scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags, dwExtraInfo = Signature },
        },
    };

    static bool IsExtendedKey(int vk) => vk switch
    {
        VK_RMENU or VK_RCONTROL or VK_LWIN or VK_RWIN => true,
        (int)Keys.Apps or (int)Keys.Insert or (int)Keys.Delete or (int)Keys.Home or (int)Keys.End
            or (int)Keys.PageUp or (int)Keys.PageDown or (int)Keys.Left or (int)Keys.Right
            or (int)Keys.Up or (int)Keys.Down or (int)Keys.NumLock or (int)Keys.PrintScreen
            or (int)Keys.Divide => true,
        >= (int)Keys.BrowserBack and <= (int)Keys.LaunchApplication2 => true,
        _ => false,
    };
}
