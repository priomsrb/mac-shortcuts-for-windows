using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static MacShortcuts.Interop.Kernel32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.Remapping;

/// <summary>
/// Installs low-level keyboard and mouse hooks on a dedicated thread with its own message loop,
/// so input keeps flowing even while the UI thread is busy. Only one instance can run at a time,
/// since the native callbacks have no context to find it by.
///
/// Windows holds up all input while a low-level hook callback runs, so callbacks must not
/// inject input themselves: injecting a mouse event from the mouse hook stalls until the hook
/// times out, and Windows silently removes hooks that time out repeatedly. Use <see cref="Defer"/>.
/// </summary>
internal sealed unsafe class LowLevelHooks : IDisposable
{
    /// <returns>true to swallow the event.</returns>
    public delegate bool KeyHandler(in KBDLLHOOKSTRUCT key);

    /// <returns>true to swallow the event.</returns>
    public delegate bool MouseHandler(IntPtr message, in MSLLHOOKSTRUCT mouse);

    const int WM_RUN_DEFERRED = WM_APP + 1;

    static LowLevelHooks? Current;

    readonly KeyHandler _onKey;
    readonly MouseHandler _onMouse;
    readonly ConcurrentQueue<Action> _deferred = new();
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

            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WM_RUN_DEFERRED) RunDeferred();
            }

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

    /// <summary>Runs <paramref name="action"/> on the hook thread once the current callback has returned.</summary>
    public void Defer(Action action)
    {
        _deferred.Enqueue(action);
        PostThreadMessage(_threadId, WM_RUN_DEFERRED, IntPtr.Zero, IntPtr.Zero);
    }

    void RunDeferred()
    {
        while (_deferred.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
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
