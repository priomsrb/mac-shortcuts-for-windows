using System.Diagnostics;
using System.Runtime.InteropServices;
using MacShortcuts.Interop;
using MacShortcuts.Shortcuts;
using static MacShortcuts.Interop.Kernel32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.Remapping;

/// <summary>
/// Installs low-level keyboard and mouse hooks on a dedicated thread and rewrites
/// Alt+Key presses into their Windows equivalents.
///
/// How Alt is handled: Alt key-downs are passed through untouched, so unmapped combos
/// (Alt+Tab, Alt+F4, ...) keep working. When a mapped key is pressed, the original key is
/// swallowed, Alt is released *logically* (after tapping an unassigned "mask" key so the
/// menu bar doesn't activate), and the replacement keys are injected. While Alt is still
/// physically held but logically released, any unmapped key re-presses Alt first, and the
/// eventual physical Alt key-up is swallowed.
/// </summary>
public sealed class KeyRemapper : IDisposable
{
    // Tags input we inject so our own hooks ignore it.
    static readonly IntPtr Signature = new(0x4D414353); // "MACS"

    // Unassigned virtual key tapped before releasing Alt so Windows doesn't open the menu bar.
    const int MaskKey = 0xE8;

    // On AltGr layouts, Right Alt also generates a fake Left Ctrl with this scan code.
    const uint AltGrFakeCtrlScanCode = 0x21D;

    static readonly int InputSize = Marshal.SizeOf<INPUT>();

    volatile RemapConfig _config = new(false, false, false, false,
        new Dictionary<Trigger, ShortcutAction>(), new HashSet<string>());
    volatile bool _resetRequested;

    // Kept in fields so the delegates outlive the native hooks that call them.
    readonly HookProc _keyboardProc;
    readonly HookProc _mouseProc;
    Thread? _thread;
    uint _threadId;

    // Everything below is only touched on the hook thread.
    bool _lAlt, _rAlt;               // physically held
    bool _lAltLogical, _rAltLogical; // what Windows currently thinks
    bool _lShift, _rShift, _lCtrl, _rCtrl, _lWin, _rWin;
    bool _ctrlClickActive;
    readonly HashSet<int> _swallowedKeyUps = [];
    readonly Dictionary<uint, string> _processNames = [];

    public KeyRemapper()
    {
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

    public void Update(RemapConfig config)
    {
        _config = config;
        _resetRequested = true;
    }

    /// <summary>Forget tracked key state, e.g. after the session was locked and key-ups were missed.</summary>
    public void ResetKeyState() => _resetRequested = true;

    public void Dispose()
    {
        if (_thread == null) return;
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
        _thread = null;
    }

    bool Shift => _lShift || _rShift;
    bool Ctrl => _lCtrl || _rCtrl;
    bool Win => _lWin || _rWin;
    bool AltAsCmd(RemapConfig cfg) => (_lAlt && cfg.LeftAlt) || (_rAlt && cfg.RightAlt);

    IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (k.dwExtraInfo != Signature)
            {
                try
                {
                    if (HandleKey(k)) return 1;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_LBUTTONDOWN || wParam == WM_LBUTTONUP))
        {
            var m = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if (m.dwExtraInfo != Signature)
            {
                try
                {
                    if (HandleClick(wParam == WM_LBUTTONDOWN, m.pt)) return 1;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    /// <returns>true to swallow the key event.</returns>
    bool HandleKey(in KBDLLHOOKSTRUCT k)
    {
        if (_resetRequested) ResetState();

        bool up = (k.flags & LLKHF_UP) != 0;
        int vk = (int)k.vkCode;

        switch (vk)
        {
            case VK_LMENU: return HandleAlt(ref _lAlt, ref _lAltLogical, up);
            case VK_RMENU: return HandleAlt(ref _rAlt, ref _rAltLogical, up);
            case VK_LSHIFT: _lShift = !up; return false;
            case VK_RSHIFT: _rShift = !up; return false;
            case VK_LCONTROL:
                if (k.scanCode != AltGrFakeCtrlScanCode) _lCtrl = !up;
                return false;
            case VK_RCONTROL: _rCtrl = !up; return false;
            case VK_LWIN: _lWin = !up; return false;
            case VK_RWIN: _rWin = !up; return false;
        }

        if (up) return _swallowedKeyUps.Remove(vk);

        ReconcileModifiers();
        var cfg = _config;
        if (cfg.Enabled && AltAsCmd(cfg) && !Ctrl && !Win
            && cfg.Map.TryGetValue(new Trigger((Keys)vk, Shift), out var action)
            && !IsExcluded(GetForegroundWindow(), cfg))
        {
            Execute(action);
            _swallowedKeyUps.Add(vk);
            return true;
        }

        return ReinjectWithAlt(k);
    }

    static bool HandleAlt(ref bool physical, ref bool logical, bool up)
    {
        if (!up)
        {
            bool repeat = physical;
            physical = true;
            // Auto-repeat after we released Alt logically: keep it released.
            if (repeat && !logical) return true;
            logical = true;
            return false;
        }

        physical = false;
        if (logical)
        {
            logical = false;
            return false;
        }
        // We already released it; Windows doesn't need a second key-up.
        return true;
    }

    bool HandleClick(bool down, POINT pt)
    {
        if (down)
        {
            ReconcileModifiers();
            var cfg = _config;
            if (!cfg.Enabled || !cfg.CtrlClick || !AltAsCmd(cfg) || Ctrl || Win
                || IsExcluded(WindowFromPoint(pt), cfg))
                return false;

            var inputs = new List<INPUT>();
            ReleaseAlt(inputs);
            AddKey(inputs, VK_LCONTROL, up: false);
            AddMouse(inputs, MOUSEEVENTF_LEFTDOWN);
            Send(inputs);
            _ctrlClickActive = true;
            return true;
        }

        if (!_ctrlClickActive) return false;
        _ctrlClickActive = false;

        var ups = new List<INPUT>();
        AddMouse(ups, MOUSEEVENTF_LEFTUP);
        if (!Ctrl) AddKey(ups, VK_LCONTROL, up: true);
        Send(ups);
        return true;
    }

    void Execute(ShortcutAction action)
    {
        var inputs = new List<INPUT>();
        ReleaseAlt(inputs);

        if (action is SendKeysAction send)
        {
            // Ctrl and Win are never held here (we don't remap when they are), so only Shift needs restoring.
            Mods held = Shift ? Mods.Shift : Mods.None;
            Mods current = held;
            foreach (var chord in send.Chords)
            {
                SetMods(inputs, ref current, chord.Mods);
                AddKey(inputs, (int)chord.Key, up: false);
                AddKey(inputs, (int)chord.Key, up: true);
            }
            SetMods(inputs, ref current, held);
        }

        Send(inputs);

        if (action is MinimizeWindowAction)
            PostMessage(GetForegroundWindow(), WM_SYSCOMMAND, SC_MINIMIZE, IntPtr.Zero);
    }

    /// <summary>Release Alt in Windows' eyes while the user keeps holding it.</summary>
    void ReleaseAlt(List<INPUT> inputs)
    {
        if (!_lAltLogical && !_rAltLogical) return;
        AddKey(inputs, MaskKey, up: false);
        AddKey(inputs, MaskKey, up: true);
        if (_lAltLogical) { AddKey(inputs, VK_LMENU, up: true); _lAltLogical = false; }
        if (_rAltLogical) { AddKey(inputs, VK_RMENU, up: true); _rAltLogical = false; }
    }

    /// <summary>
    /// The user pressed an unmapped key while holding Alt after we'd released it logically
    /// (e.g. Alt+C then Alt+Tab). Re-press Alt and replay the key so the combo works.
    /// </summary>
    bool ReinjectWithAlt(in KBDLLHOOKSTRUCT k)
    {
        bool left = _lAlt && !_lAltLogical;
        bool right = _rAlt && !_rAltLogical;
        if (!left && !right) return false;

        var inputs = new List<INPUT>();
        if (left) { AddKey(inputs, VK_LMENU, up: false); _lAltLogical = true; }
        if (right) { AddKey(inputs, VK_RMENU, up: false); _rAltLogical = true; }
        inputs.Add(KeyInput((ushort)k.vkCode, (ushort)k.scanCode,
            (k.flags & LLKHF_EXTENDED) != 0 ? KEYEVENTF_EXTENDEDKEY : 0));
        Send(inputs);
        return true;
    }

    void SetMods(List<INPUT> inputs, ref Mods current, Mods target)
    {
        SetMod(inputs, current, target, Mods.Ctrl, VK_LCONTROL, VK_RCONTROL, _lCtrl, _rCtrl);
        SetMod(inputs, current, target, Mods.Shift, VK_LSHIFT, VK_RSHIFT, _lShift, _rShift);
        SetMod(inputs, current, target, Mods.Win, VK_LWIN, VK_RWIN, _lWin, _rWin);
        // Alt was already released logically; always use Left Alt when a chord needs it.
        SetMod(inputs, current, target, Mods.Alt, VK_LMENU, VK_RMENU, false, false);
        current = target;
    }

    static void SetMod(List<INPUT> inputs, Mods current, Mods target, Mods mod,
        int leftVk, int rightVk, bool leftHeld, bool rightHeld)
    {
        bool isDown = current.HasFlag(mod);
        bool wantDown = target.HasFlag(mod);
        if (isDown == wantDown) return;
        // Act on the physically held side(s) so Windows' state matches the keyboard afterwards.
        if (rightHeld) AddKey(inputs, rightVk, up: !wantDown);
        if (leftHeld || !rightHeld) AddKey(inputs, leftVk, up: !wantDown);
    }

    /// <summary>Clear modifiers whose key-up we missed (e.g. released on the lock screen).</summary>
    void ReconcileModifiers()
    {
        static void Fix(ref bool held, int vk)
        {
            if (held && (GetAsyncKeyState(vk) & 0x8000) == 0) held = false;
        }

        Fix(ref _lShift, VK_LSHIFT);
        Fix(ref _rShift, VK_RSHIFT);
        Fix(ref _lCtrl, VK_LCONTROL);
        Fix(ref _rCtrl, VK_RCONTROL);
        Fix(ref _lWin, VK_LWIN);
        Fix(ref _rWin, VK_RWIN);
        // Only verifiable while Windows still thinks Alt is down.
        if (_lAltLogical) { Fix(ref _lAlt, VK_LMENU); _lAltLogical = _lAlt; }
        if (_rAltLogical) { Fix(ref _rAlt, VK_RMENU); _rAltLogical = _rAlt; }
    }

    void ResetState()
    {
        _resetRequested = false;
        _lAlt = _rAlt = _lAltLogical = _rAltLogical = false;
        _lShift = _rShift = _lCtrl = _rCtrl = _lWin = _rWin = false;
        _swallowedKeyUps.Clear();
        _processNames.Clear();
    }

    bool IsExcluded(IntPtr hwnd, RemapConfig cfg)
    {
        if (cfg.ExcludedApps.Count == 0 || hwnd == IntPtr.Zero) return false;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (!_processNames.TryGetValue(pid, out var name))
        {
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
        }
        return cfg.ExcludedApps.Contains(name);
    }

    static void AddKey(List<INPUT> inputs, int vk, bool up)
    {
        uint flags = (up ? KEYEVENTF_KEYUP : 0) | (IsExtendedKey(vk) ? KEYEVENTF_EXTENDEDKEY : 0);
        inputs.Add(KeyInput((ushort)vk, (ushort)MapVirtualKey((uint)vk, 0), flags));
    }

    static INPUT KeyInput(ushort vk, ushort scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags, dwExtraInfo = Signature },
        },
    };

    static void AddMouse(List<INPUT> inputs, uint flags) => inputs.Add(new INPUT
    {
        type = INPUT_MOUSE,
        U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags, dwExtraInfo = Signature } },
    });

    static void Send(List<INPUT> inputs)
    {
        if (inputs.Count > 0) SendInput((uint)inputs.Count, inputs.ToArray(), InputSize);
    }

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
