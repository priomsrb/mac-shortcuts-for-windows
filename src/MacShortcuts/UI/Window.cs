using System.ComponentModel;
using System.Runtime.InteropServices;
using static MacShortcuts.Interop.Kernel32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI;

/// <summary>
/// A top-level Win32 window whose messages are routed to <see cref="WndProc"/>.
/// The window keeps this object alive (through a GC handle) until it's destroyed.
/// </summary>
internal abstract unsafe class Window
{
    protected static readonly IntPtr Instance = GetModuleHandle(null);

    static readonly HashSet<string> RegisteredClasses = [];

    /// <summary>The native window, or zero before it's created and after it's destroyed.</summary>
    public IntPtr Handle { get; private set; }

    protected void CreateHandle(string className, string? title, uint style, uint exStyle)
    {
        if (RegisteredClasses.Add(className))
        {
            fixed (char* name = className)
            {
                var wc = new WNDCLASSEXW
                {
                    cbSize = (uint)sizeof(WNDCLASSEXW),
                    lpfnWndProc = &StaticWndProc,
                    hInstance = Instance,
                    hCursor = LoadCursor(IntPtr.Zero, IDC_ARROW),
                    lpszClassName = name,
                };
                if (RegisterClassEx(wc) == 0) throw new Win32Exception();
            }
        }

        var self = GCHandle.Alloc(this);
        const int CW_USEDEFAULT = unchecked((int)0x80000000);
        IntPtr hwnd = CreateWindowEx(exStyle, className, title, style,
            CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT,
            IntPtr.Zero, IntPtr.Zero, Instance, GCHandle.ToIntPtr(self));
        if (hwnd == IntPtr.Zero)
        {
            if (self.IsAllocated) self.Free();
            throw new Win32Exception();
        }
    }

    protected virtual IntPtr WndProc(uint msg, IntPtr wParam, IntPtr lParam) =>
        DefWindowProc(Handle, msg, wParam, lParam);

    /// <summary>Called once the window and its children are gone.</summary>
    protected virtual void OnHandleDestroyed() { }

    [UnmanagedCallersOnly]
    static IntPtr StaticWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_NCCREATE)
        {
            IntPtr param = ((CREATESTRUCTW*)lParam)->lpCreateParams;
            SetWindowLongPtr(hwnd, GWLP_USERDATA, param);
            ((Window)GCHandle.FromIntPtr(param).Target!).Handle = hwnd;
        }

        IntPtr stored = GetWindowLongPtr(hwnd, GWLP_USERDATA);
        // Messages sent before WM_NCCREATE, e.g. WM_GETMINMAXINFO.
        if (stored == IntPtr.Zero) return DefWindowProc(hwnd, msg, wParam, lParam);

        var handle = GCHandle.FromIntPtr(stored);
        var window = (Window)handle.Target!;
        if (msg == WM_NCDESTROY)
        {
            SetWindowLongPtr(hwnd, GWLP_USERDATA, IntPtr.Zero);
            handle.Free();
            window.Handle = IntPtr.Zero;
            window.OnHandleDestroyed();
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }

        try
        {
            return window.WndProc(msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            // Exceptions can't unwind through native code; report and carry on like WinForms did.
            MessageBox(hwnd, ex.ToString(), "Mac Shortcuts for Windows", MB_OK | MB_ICONWARNING);
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }
    }
}
