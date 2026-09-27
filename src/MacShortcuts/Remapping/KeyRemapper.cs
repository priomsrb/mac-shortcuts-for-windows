using static MacShortcuts.Interop.User32;

namespace MacShortcuts.Remapping;

/// <summary>
/// Rewrites Alt+Key presses into their Windows equivalents: wires the low-level hooks to
/// <see cref="RemapEngine"/>, which injects its output through <see cref="WindowsInputSystem"/>.
/// </summary>
public sealed class KeyRemapper : IDisposable
{
    readonly WindowsInputSystem _system = new();
    readonly RemapEngine _engine;
    readonly LowLevelHooks _hooks;

    public KeyRemapper()
    {
        _engine = new RemapEngine(_system);
        _hooks = new LowLevelHooks(OnKey, OnMouse);
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
            k.scanCode, Extended: (k.flags & LLKHF_EXTENDED) != 0));

    bool OnMouse(IntPtr message, in MSLLHOOKSTRUCT m) =>
        (message == WM_LBUTTONDOWN || message == WM_LBUTTONUP)
        && m.dwExtraInfo != WindowsInputSystem.Signature
        && _engine.OnLeftButton(down: message == WM_LBUTTONDOWN, m.pt);
}
