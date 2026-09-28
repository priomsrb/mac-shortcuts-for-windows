using MacShortcuts.Interop;
using MacShortcuts.Shortcuts;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.Remapping;

/// <summary>
/// Decides what to do with each physical key and click: pass it through, or swallow it and
/// inject replacement input.
///
/// How Alt is handled: Alt key-downs are passed through untouched, so unmapped combos
/// (Alt+Tab, Alt+F4, ...) keep working. When a mapped key is pressed, the original key is
/// swallowed, Alt is released *logically* (after tapping an unassigned "mask" key so the
/// menu bar doesn't activate), and the replacement keys are injected. While Alt is still
/// physically held but logically released, any unmapped key re-presses Alt first, and the
/// eventual physical Alt key-up is swallowed.
///
/// <see cref="OnKey"/>, <see cref="OnLeftButton"/> and <see cref="OnWheel"/> must be called from a single thread;
/// <see cref="Update"/> and <see cref="ResetKeyState"/> may be called from any thread.
/// </summary>
internal sealed class RemapEngine(IInputSystem system)
{
    // Unassigned virtual key tapped before releasing Alt so Windows doesn't open the menu bar.
    public const int MaskKey = 0xE8;

    // On AltGr layouts, Right Alt also generates a fake Left Ctrl with this scan code.
    public const uint AltGrFakeCtrlScanCode = 0x21D;

    volatile RemapConfig _config = RemapConfig.Disabled;
    volatile bool _resetRequested;

    // Everything below is only touched on the input thread.
    bool _lAlt, _rAlt;               // physically held
    bool _lAltLogical, _rAltLogical; // what Windows currently thinks
    bool _lShift, _rShift, _lCtrl, _rCtrl, _lWin, _rWin;
    bool _ctrlClickActive;
    bool _altTabActive;              // Alt+Tab switcher is (probably) open; leave clicks alone
    readonly HashSet<int> _swallowedKeyUps = [];

    public void Update(RemapConfig config)
    {
        _config = config;
        _resetRequested = true;
    }

    /// <summary>Forget tracked key state, e.g. after the session was locked and key-ups were missed.</summary>
    public void ResetKeyState() => _resetRequested = true;

    bool Shift => _lShift || _rShift;
    bool Ctrl => _lCtrl || _rCtrl;
    bool Win => _lWin || _rWin;
    bool AltAsCmd(RemapConfig cfg) => (_lAlt && cfg.LeftAlt) || (_rAlt && cfg.RightAlt);

    /// <returns>true to swallow the key event.</returns>
    public bool OnKey(KeyEvent k)
    {
        if (_resetRequested) ResetState();

        switch (k.Vk)
        {
            case VK_LMENU: return OnAlt(ref _lAlt, ref _lAltLogical, k.Up);
            case VK_RMENU: return OnAlt(ref _rAlt, ref _rAltLogical, k.Up);
            case VK_LSHIFT: _lShift = !k.Up; return false;
            case VK_RSHIFT: _rShift = !k.Up; return false;
            case VK_LCONTROL:
                if (k.ScanCode != AltGrFakeCtrlScanCode) _lCtrl = !k.Up;
                return false;
            case VK_RCONTROL: _rCtrl = !k.Up; return false;
            case VK_LWIN: _lWin = !k.Up; return false;
            case VK_RWIN: _rWin = !k.Up; return false;
        }

        if (k.Up) return _swallowedKeyUps.Remove(k.Vk);

        ReconcileModifiers();
        var cfg = _config;
        if (cfg.Enabled && AltAsCmd(cfg) && !Ctrl && !Win
            && cfg.Map.TryGetValue(new Trigger((Keys)k.Vk, Shift), out var action)
            && !IsExcluded(cfg, system.GetForegroundProcessName))
        {
            Execute(action);
            _swallowedKeyUps.Add(k.Vk);
            return true;
        }

        // Clicking a window in the Alt+Tab switcher needs a plain click with Alt still held.
        if (k.Vk == (int)Keys.Tab && (_lAlt || _rAlt)) _altTabActive = true;

        return ReinjectWithAlt(k);
    }

    /// <returns>true to swallow the mouse event.</returns>
    public bool OnLeftButton(bool down, POINT point)
    {
        if (down)
        {
            ReconcileModifiers();
            var cfg = _config;
            if (!cfg.Enabled || !cfg.CtrlClick || !AltAsCmd(cfg) || Ctrl || Win || _altTabActive
                || IsExcluded(cfg, () => system.GetProcessNameAt(point)))
                return false;

            var inputs = new List<SyntheticInput>();
            ReleaseAlt(inputs);
            inputs.Add(new SyntheticInput.Key(VK_LCONTROL, Up: false));
            inputs.Add(new SyntheticInput.LeftButton(Up: false));
            system.Send(inputs);
            _ctrlClickActive = true;
            return true;
        }

        if (!_ctrlClickActive) return false;
        _ctrlClickActive = false;

        var ups = new List<SyntheticInput> { new SyntheticInput.LeftButton(Up: true) };
        if (!Ctrl) ups.Add(new SyntheticInput.Key(VK_LCONTROL, Up: true));
        system.Send(ups);
        return true;
    }

    /// <returns>true to swallow the wheel event.</returns>
    public bool OnWheel(int delta, POINT point)
    {
        ReconcileModifiers();
        var cfg = _config;
        if (!cfg.Enabled || !cfg.CtrlScroll || !AltAsCmd(cfg) || Ctrl || Win || _altTabActive
            || IsExcluded(cfg, () => system.GetProcessNameAt(point)))
            return false;

        var inputs = new List<SyntheticInput>();
        ReleaseAlt(inputs);
        // During an Alt+Click, Ctrl is already down and must stay down until the button is released.
        if (!_ctrlClickActive) inputs.Add(new SyntheticInput.Key(VK_LCONTROL, Up: false));
        inputs.Add(new SyntheticInput.Wheel(delta));
        if (!_ctrlClickActive) inputs.Add(new SyntheticInput.Key(VK_LCONTROL, Up: true));
        system.Send(inputs);
        return true;
    }

    bool OnAlt(ref bool physical, ref bool logical, bool up)
    {
        bool swallow = HandleAlt(ref physical, ref logical, up);
        if (!_lAlt && !_rAlt) _altTabActive = false;
        return swallow;
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

    void Execute(ShortcutAction action)
    {
        var inputs = new List<SyntheticInput>();
        ReleaseAlt(inputs);

        if (action is SendKeysAction send)
        {
            // Ctrl and Win are never held here (we don't remap when they are), so only Shift needs restoring.
            Mods held = Shift ? Mods.Shift : Mods.None;
            Mods current = held;
            foreach (var chord in send.Chords)
            {
                SetMods(inputs, ref current, chord.Mods);
                inputs.Add(new SyntheticInput.Key((int)chord.Key, Up: false));
                inputs.Add(new SyntheticInput.Key((int)chord.Key, Up: true));
            }
            SetMods(inputs, ref current, held);
        }

        system.Send(inputs);

        if (action is MinimizeWindowAction)
            system.MinimizeForegroundWindow();
    }

    /// <summary>Release Alt in Windows' eyes while the user keeps holding it.</summary>
    void ReleaseAlt(List<SyntheticInput> inputs)
    {
        if (!_lAltLogical && !_rAltLogical) return;
        inputs.Add(new SyntheticInput.Key(MaskKey, Up: false));
        inputs.Add(new SyntheticInput.Key(MaskKey, Up: true));
        if (_lAltLogical) { inputs.Add(new SyntheticInput.Key(VK_LMENU, Up: true)); _lAltLogical = false; }
        if (_rAltLogical) { inputs.Add(new SyntheticInput.Key(VK_RMENU, Up: true)); _rAltLogical = false; }
    }

    /// <summary>
    /// The user pressed an unmapped key while holding Alt after we'd released it logically
    /// (e.g. Alt+C then Alt+Tab). Re-press Alt and replay the key so the combo works.
    /// </summary>
    bool ReinjectWithAlt(KeyEvent k)
    {
        bool left = _lAlt && !_lAltLogical;
        bool right = _rAlt && !_rAltLogical;
        if (!left && !right) return false;

        var inputs = new List<SyntheticInput>();
        if (left) { inputs.Add(new SyntheticInput.Key(VK_LMENU, Up: false)); _lAltLogical = true; }
        if (right) { inputs.Add(new SyntheticInput.Key(VK_RMENU, Up: false)); _rAltLogical = true; }
        inputs.Add(new SyntheticInput.Replay(k));
        system.Send(inputs);
        return true;
    }

    void SetMods(List<SyntheticInput> inputs, ref Mods current, Mods target)
    {
        SetMod(inputs, current, target, Mods.Ctrl, VK_LCONTROL, VK_RCONTROL, _lCtrl, _rCtrl);
        SetMod(inputs, current, target, Mods.Shift, VK_LSHIFT, VK_RSHIFT, _lShift, _rShift);
        SetMod(inputs, current, target, Mods.Win, VK_LWIN, VK_RWIN, _lWin, _rWin);
        // Alt was already released logically; always use Left Alt when a chord needs it.
        SetMod(inputs, current, target, Mods.Alt, VK_LMENU, VK_RMENU, false, false);
        current = target;
    }

    static void SetMod(List<SyntheticInput> inputs, Mods current, Mods target, Mods mod,
        int leftVk, int rightVk, bool leftHeld, bool rightHeld)
    {
        bool isDown = current.HasFlag(mod);
        bool wantDown = target.HasFlag(mod);
        if (isDown == wantDown) return;
        // Act on the physically held side(s) so Windows' state matches the keyboard afterwards.
        if (rightHeld) inputs.Add(new SyntheticInput.Key(rightVk, Up: !wantDown));
        if (leftHeld || !rightHeld) inputs.Add(new SyntheticInput.Key(leftVk, Up: !wantDown));
    }

    /// <summary>Clear modifiers whose key-up we missed (e.g. released on the lock screen).</summary>
    void ReconcileModifiers()
    {
        void Fix(ref bool held, int vk)
        {
            if (held && !system.IsKeyDown(vk)) held = false;
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
        if (!_lAlt && !_rAlt) _altTabActive = false;
    }

    void ResetState()
    {
        _resetRequested = false;
        _lAlt = _rAlt = _lAltLogical = _rAltLogical = false;
        _lShift = _rShift = _lCtrl = _rCtrl = _lWin = _rWin = false;
        _altTabActive = false;
        _swallowedKeyUps.Clear();
    }

    /// <param name="processName">Only called when there are exclusions, since looking it up is slow.</param>
    static bool IsExcluded(RemapConfig cfg, Func<string> processName) =>
        cfg.ExcludedApps.Count > 0 && cfg.ExcludedApps.Contains(processName());
}
