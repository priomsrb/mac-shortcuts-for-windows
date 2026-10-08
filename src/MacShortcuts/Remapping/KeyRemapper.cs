using static MacShortcuts.Interop.User32;

namespace MacShortcuts.Remapping;

/// <summary>
/// Rewrites Alt+Key presses into their Windows equivalents: wires the low-level hooks to
/// <see cref="RemapEngine"/>, which injects its output through <see cref="WindowsInputSystem"/>.
/// </summary>
public sealed class KeyRemapper : IDisposable
{
    readonly WindowsInputSystem _system;
    readonly RemapEngine _engine;
    readonly LowLevelHooks _hooks;

    public KeyRemapper()
    {
        _hooks = new LowLevelHooks(OnKey, OnMouse);
        _system = new WindowsInputSystem(_hooks.Defer);
        _engine = new RemapEngine(_system);
    }

    public void Start() => _hooks.Start();

    public void Update(RemapConfig config)
    {
        _engine.Update(config);
        _system.ClearProcessNames();
    }

    /// <inheritdoc cref="RemapEngine.ResetKeyState"/>
    public void ResetKeyState()
    {
        _engine.ResetKeyState();
        _system.ClearProcessNames();
    }

    public void Dispose() => _hooks.Dispose();

    bool OnKey(in KBDLLHOOKSTRUCT k) =>
        k.dwExtraInfo != WindowsInputSystem.Signature
        && _engine.OnKey(new KeyEvent((int)k.vkCode, Up: (k.flags & LLKHF_UP) != 0,
            k.scanCode, Extended: (k.flags & LLKHF_EXTENDED) != 0, Injected: (k.flags & LLKHF_INJECTED) != 0));

    bool OnMouse(IntPtr message, in MSLLHOOKSTRUCT m)
    {
        if (m.dwExtraInfo == WindowsInputSystem.Signature) return false;
        if (message == WM_LBUTTONDOWN || message == WM_LBUTTONUP)
            return _engine.OnLeftButton(down: message == WM_LBUTTONDOWN, m.pt);
        if (message == WM_MOUSEWHEEL)
            return _engine.OnWheel((short)(m.mouseData >> 16), m.pt); // The high word is the signed delta.
        return false;
    }
}
