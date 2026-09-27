using System.Diagnostics;
using System.Runtime.InteropServices;
using static MacShortcuts.Interop.Kernel32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.Remapping;

/// <summary>
/// Installs low-level keyboard and mouse hooks on a dedicated thread with its own message loop,
/// so input keeps flowing even while the UI thread is busy. Only one instance can run at a time,
/// since the native callbacks have no context to find it by.
/// </summary>
internal sealed unsafe class LowLevelHooks : IDisposable
{
    /// <returns>true to swallow the event.</returns>
    public delegate bool KeyHandler(in KBDLLHOOKSTRUCT key);

    /// <returns>true to swallow the event.</returns>
    public delegate bool MouseHandler(IntPtr message, in MSLLHOOKSTRUCT mouse);

    static LowLevelHooks? Current;

    readonly KeyHandler _onKey;
    readonly MouseHandler _onMouse;
    Thread? _thread;
    uint _threadId;

    public LowLevelHooks(KeyHandler onKey, MouseHandler onMouse)
    {
        _onKey = onKey;
        _onMouse = onMouse;
    }

    public void Start()
    {
        if (Interlocked.CompareExchange(ref Current, this, null) != null)
            throw new InvalidOperationException("Hooks are already installed.");

        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            var module = GetModuleHandle(null);
            var kb = SetWindowsHookEx(WH_KEYBOARD_LL, &KeyboardProc, module, 0);
            var mouse = SetWindowsHookEx(WH_MOUSE_LL, &MouseProc, module, 0);
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
        Interlocked.CompareExchange(ref Current, null, this);
    }

    [UnmanagedCallersOnly]
    static IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && Current is { } hooks)
        {
            try
            {
                if (hooks._onKey(in *(KBDLLHOOKSTRUCT*)lParam)) return 1;
            }
            catch (Exception ex)
            {
                // Never let a bug block the user's keyboard.
                Debug.WriteLine(ex);
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    [UnmanagedCallersOnly]
    static IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && Current is { } hooks)
        {
            try
            {
                if (hooks._onMouse(wParam, in *(MSLLHOOKSTRUCT*)lParam)) return 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }
}
