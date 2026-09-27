using System.Diagnostics;
using System.Runtime.InteropServices;
using static MacShortcuts.Interop.Kernel32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.Remapping;

/// <summary>
/// Installs low-level keyboard and mouse hooks on a dedicated thread with its own message loop,
/// so input keeps flowing even while the UI thread is busy.
/// </summary>
internal sealed class LowLevelHooks : IDisposable
{
    /// <returns>true to swallow the event.</returns>
    public delegate bool KeyHandler(in KBDLLHOOKSTRUCT key);

    /// <returns>true to swallow the event.</returns>
    public delegate bool MouseHandler(IntPtr message, in MSLLHOOKSTRUCT mouse);

    readonly KeyHandler _onKey;
    readonly MouseHandler _onMouse;

    // Kept in fields so the delegates outlive the native hooks that call them.
    readonly HookProc _keyboardProc;
    readonly HookProc _mouseProc;
    Thread? _thread;
    uint _threadId;

    public LowLevelHooks(KeyHandler onKey, MouseHandler onMouse)
    {
        _onKey = onKey;
        _onMouse = onMouse;
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
    }

    public void Start()
    {
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            var module = GetModuleHandle(null);
            var kb = SetWindowsHookEx(WH_KEYBOARD_LL, Marshal.GetFunctionPointerForDelegate(_keyboardProc), module, 0);
            var mouse = SetWindowsHookEx(WH_MOUSE_LL, Marshal.GetFunctionPointerForDelegate(_mouseProc), module, 0);
            ready.Set();

            while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }

            UnhookWindowsHookEx(kb);
            UnhookWindowsHookEx(mouse);
        })
        {
            IsBackground = true,
            Name = "MacShortcuts hook thread",
            Priority = ThreadPriority.Highest,
        };
        _thread.Start();
        ready.Wait();
    }

    public void Dispose()
    {
        if (_thread == null) return;
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
        _thread = null;
    }

    IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            try
            {
                if (_onKey(k)) return 1;
            }
            catch (Exception ex)
            {
                // Never let a bug block the user's keyboard.
                Debug.WriteLine(ex);
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var m = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            try
            {
                if (_onMouse(wParam, m)) return 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }
}
