using MacShortcuts.Interop;
using MacShortcuts.Platform;
using MacShortcuts.Settings;
using MacShortcuts.Shortcuts;
using MacShortcuts.UI.Controls;
using static MacShortcuts.Interop.ComCtl32;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI;

/// <summary>The settings window. Closing it only hides it; the app keeps running in the tray.</summary>
internal sealed unsafe class MainWindow : Window
{
    const string Title = "Mac Shortcuts for Windows";
    const uint Style = WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN;
    const uint ExStyle = WS_EX_CONTROLPARENT;
    const int WM_SET_THEME = WM_APP + 1;
    const int WM_REDRAW_CHECKBOXES = WM_APP + 2;
    const nuint ExcludedSaveTimer = 1;

    readonly SettingsController _settings;
    readonly Palette _palette;
    readonly IntPtr _windowBrush;
    readonly IntPtr _controlBrush;
    IntPtr _font;
    IntPtr _boldFont;
    int _dpi = 96;
    int _textHeight;
    bool _loading;
    bool _created;

    readonly IntPtr _enabled;
    readonly IntPtr _leftAlt;
    readonly IntPtr _rightAlt;
    readonly IntPtr _startup;
    readonly IntPtr _themeLabel;
    readonly IntPtr _theme;
    readonly IntPtr _admin;
    readonly ListView _list;
    readonly IntPtr _excludedHint;
    readonly IntPtr _excluded;
    readonly IntPtr[] _buttons;
    readonly IntPtr _buttonsHint;
    readonly IntPtr _tooltip;

    // Set by Layout() for WM_PAINT.
    RECT _excludedGroup;
    RECT _listFrame;
    RECT _excludedFrame;

    /// <summary>The user closed the window; the app keeps running in the tray.</summary>
    public event EventHandler? HiddenToTray;

    public event EventHandler? RestartAsAdminRequested;

    /// <param name="placement">Where to show the window (e.g. to replace one with the old theme), or null to centre it.</param>
    public MainWindow(SettingsController settings, bool dark, WINDOWPLACEMENT? placement)
    {
        _settings = settings;
        _palette = Palette.For(dark);
        _windowBrush = CreateSolidBrush(_palette.Window);
        _controlBrush = CreateSolidBrush(_palette.Control);

        CreateHandle("MacShortcuts.Settings", Title, Style, ExStyle);
        _dpi = (int)GetDpiForWindow(Handle);
        CreateFonts();
        SendMessage(Handle, WM_SETICON, ICON_BIG, AppIcons.Large);
        SendMessage(Handle, WM_SETICON, ICON_SMALL, AppIcons.Small);

        // Children are created in tab order.
        _enabled = CheckBox("Enable remapping");
        _leftAlt = CheckBox("Left Alt acts as ⌘ Cmd");
        _rightAlt = CheckBox("Right Alt acts as ⌘ Cmd");
        _startup = CheckBox("Start with Windows");
        _themeLabel = Label("Theme:");
        _theme = Child("COMBOBOX", null, CBS_DROPDOWNLIST | WS_VSCROLL | WS_TABSTOP);
        foreach (string name in Enum.GetNames<AppTheme>()) SendMessage(_theme, CB_ADDSTRING, IntPtr.Zero, name);
        _admin = Elevation.IsAdmin
            ? Label("Running as administrator")
            : Child("BUTTON", "Restart as administrator", BS_PUSHBUTTON | WS_TABSTOP);

        // The default 3D borders are near-white in dark mode; there, WM_PAINT draws subtle ones instead.
        uint edge = dark ? 0 : WS_EX_CLIENTEDGE;
        _list = new ListView(Handle, edge);
        _list.AddColumn("Mac-style shortcut", Scale(210));
        _list.AddColumn("Sends", Scale(220));
        _list.AddColumn("Action", Scale(380));
        var groups = new Dictionary<string, int>();
        foreach (var shortcut in ShortcutCatalog.All)
        {
            if (!groups.TryGetValue(shortcut.Category, out int group))
            {
                group = groups.Count + 1;
                groups[shortcut.Category] = group;
                _list.AddGroup(group, shortcut.Category);
            }
            _list.AddItem(group, shortcut.TriggerText, shortcut.SendsText, shortcut.Description);
        }

        _excludedHint = Label("Nothing is remapped while these apps are focused. One process name per line, e.g. mstsc.exe");
        _excluded = Child("EDIT", null, ES_MULTILINE | ES_AUTOVSCROLL | ES_WANTRETURN | WS_VSCROLL | WS_TABSTOP, edge);

        _buttons =
        [
            Child("BUTTON", "Enable all", BS_PUSHBUTTON | WS_TABSTOP),
            Child("BUTTON", "Disable all", BS_PUSHBUTTON | WS_TABSTOP),
            Child("BUTTON", "Reset to defaults", BS_PUSHBUTTON | WS_TABSTOP),
        ];
        _buttonsHint = Label("Changes apply immediately. Closing this window keeps the app running in the tray.");

        _tooltip = CreateWindowEx(WS_EX_TOPMOST, "tooltips_class32", null, WS_POPUP | TTS_ALWAYSTIP | TTS_NOPREFIX,
            0, 0, 0, 0, Handle, IntPtr.Zero, Instance, IntPtr.Zero);
        AddTooltip(_rightAlt, "On keyboard layouts with AltGr, this replaces AltGr+key characters (e.g. €).");
        if (!Elevation.IsAdmin)
            AddTooltip(_admin, "Needed for shortcuts to work while an admin app (Task Manager, admin terminal…) is focused.");

        ApplyFonts();
        ApplyTheme();
        LoadFromSettings();
        _settings.Changed += OnSettingsChanged;

        if (placement is { } p)
        {
            var copy = p with { length = (uint)sizeof(WINDOWPLACEMENT) };
            SetWindowPlacement(Handle, copy);
        }
        else
        {
            CenterOnScreen();
        }
        _created = true;
        Layout();
    }

    public bool IsVisible => Handle != IntPtr.Zero && IsWindowVisible(Handle);

    public WINDOWPLACEMENT Placement
    {
        get
        {
            var placement = new WINDOWPLACEMENT { length = (uint)sizeof(WINDOWPLACEMENT) };
            GetWindowPlacement(Handle, ref placement);
            return placement;
        }
    }

    public void Show()
    {
        ShowWindow(Handle, IsIconic(Handle) ? SW_RESTORE : SW_SHOW);
        SetForegroundWindow(Handle);
    }

    public void Destroy()
    {
        if (Handle != IntPtr.Zero) DestroyWindow(Handle);
    }

    protected override IntPtr WndProc(uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_CLOSE:
                SaveExcluded();
                ShowWindow(Handle, SW_HIDE);
                HiddenToTray?.Invoke(this, EventArgs.Empty);
                return 0;

            case WM_DESTROY:
                // The window is destroyed without closing when the theme changes or the app exits.
                SaveExcluded();
                _settings.Changed -= OnSettingsChanged;
                break;

            case WM_SIZE:
                Layout();
                return 0;

            case WM_GETMINMAXINFO:
                ((MINMAXINFO*)lParam)->ptMinTrackSize = new POINT { X = Scale(640), Y = Scale(480) };
                return 0;

            case WM_DPICHANGED:
                OnDpiChanged(LoWord(wParam), *(RECT*)lParam);
                return 0;

            case WM_ERASEBKGND:
                GetClientRect(Handle, out var client);
                FillRect(wParam, client, _windowBrush);
                return 1;

            case WM_PAINT:
                Paint();
                return 0;

            case WM_CTLCOLORSTATIC:
            case WM_CTLCOLORBTN:
                _ = SetTextColor(wParam, lParam == _excludedHint || lParam == _buttonsHint || (Elevation.IsAdmin && lParam == _admin)
                    ? _palette.GrayText : _palette.Text);
                _ = SetBkColor(wParam, _palette.Window);
                return _windowBrush;

            case WM_CTLCOLOREDIT:
            case WM_CTLCOLORLISTBOX:
                if (!_palette.IsDark) break;
                _ = SetTextColor(wParam, _palette.Text);
                _ = SetBkColor(wParam, _palette.Control);
                return _controlBrush;

            case WM_COMMAND:
                OnCommand(lParam, HiWord(wParam));
                return 0;

            case WM_NOTIFY:
                return OnNotify((NMHDR*)lParam);

            case WM_TIMER when wParam == (IntPtr)ExcludedSaveTimer:
                SaveExcluded();
                return 0;

            case WM_SET_THEME:
                _settings.SetTheme((AppTheme)wParam.ToInt32());
                return 0;

            case WM_THEMECHANGED:
                // Controls rebuild their theme parts too, so redraw the dark checkboxes after them.
                PostMessage(Handle, WM_REDRAW_CHECKBOXES, IntPtr.Zero, IntPtr.Zero);
                break;

            case WM_REDRAW_CHECKBOXES:
                if (_palette.IsDark) _list.UseDarkCheckBoxes(_dpi);
                return 0;
        }
        return base.WndProc(msg, wParam, lParam);
    }

    protected override void OnHandleDestroyed()
    {
        DeleteObject(_font);
        DeleteObject(_boldFont);
        DeleteObject(_windowBrush);
        DeleteObject(_controlBrush);
    }

    void OnCommand(IntPtr control, int code)
    {
        if (control == _excluded)
        {
            if (code == EN_CHANGE && !_loading) SetTimer(Handle, ExcludedSaveTimer, 600, IntPtr.Zero);
            return;
        }
        if (control == _theme)
        {
            // Deferred: applying a theme recreates this window.
            if (code == CBN_SELCHANGE && !_loading)
                PostMessage(Handle, WM_SET_THEME, SendMessage(_theme, CB_GETCURSEL, IntPtr.Zero, IntPtr.Zero), IntPtr.Zero);
            return;
        }
        if (code != BN_CLICKED || _loading) return;

        if (control == _enabled) _settings.SetEnabled(IsChecked(_enabled));
        else if (control == _leftAlt) _settings.SetUseLeftAlt(IsChecked(_leftAlt));
        else if (control == _rightAlt) _settings.SetUseRightAlt(IsChecked(_rightAlt));
        else if (control == _startup) SetStartup(IsChecked(_startup));
        else if (control == _admin && !Elevation.IsAdmin) RestartAsAdminRequested?.Invoke(this, EventArgs.Empty);
        else if (control == _buttons[0]) _settings.SetAllShortcutsEnabled(true);
        else if (control == _buttons[1]) _settings.SetAllShortcutsEnabled(false);
        else if (control == _buttons[2]) _settings.ResetShortcuts();
    }

    IntPtr OnNotify(NMHDR* header)
    {
        if (header->hwndFrom == _list.Handle)
        {
            if (header->code == LVN_ITEMCHANGED && !_loading && ListView.IsCheckToggle((NMLISTVIEW*)header))
            {
                int index = ((NMLISTVIEW*)header)->iItem;
                _settings.SetShortcutEnabled(ShortcutCatalog.All[index], _list.IsChecked(index));
            }
            else if (header->code == NM_CUSTOMDRAW && _palette.IsDark)
            {
                return _list.CustomDraw((NMLVCUSTOMDRAW*)header, _palette, _dpi);
            }
        }
        else if (header->code == NM_CUSTOMDRAW && _palette.IsDark && IsCheckBox(header->hwndFrom))
        {
            return DrawDarkCheckBox((NMCUSTOMDRAW*)header);
        }
        return 0;
    }

    /// <summary>
    /// Themed checkboxes ignore the text colour from WM_CTLCOLORSTATIC, so in dark mode their
    /// label would be black. Paint them entirely, with the dark theme's glyph.
    /// </summary>
    IntPtr DrawDarkCheckBox(NMCUSTOMDRAW* cd)
    {
        if (cd->dwDrawStage != CDDS_PREPAINT) return CDRF_DODEFAULT;

        IntPtr hwnd = cd->hdr.hwndFrom;
        IntPtr hdc = cd->hdc;
        FillRect(hdc, cd->rc, _windowBrush);
        int glyph = DarkCheckBoxes.DrawGlyph(hdc, cd->rc, IsChecked(hwnd),
            hot: (cd->uItemState & CDIS_HOT) != 0, pressed: (cd->uItemState & CDIS_SELECTED) != 0, _dpi);
        if (glyph == 0) return CDRF_DODEFAULT;

        IntPtr oldFont = SelectObject(hdc, hwnd == _enabled ? _boldFont : _font);
        string text = GetText(hwnd);
        var textRect = cd->rc with { left = cd->rc.left + glyph + Scale(4) };
        _ = SetBkMode(hdc, TRANSPARENT);
        _ = SetTextColor(hdc, _palette.Text);
        DrawText(hdc, text, text.Length, ref textRect, DT_SINGLELINE | DT_VCENTER | DT_NOPREFIX);
        if ((cd->uItemState & CDIS_FOCUS) != 0 && (cd->uItemState & CDIS_SHOWKEYBOARDCUES) != 0)
        {
            var focus = textRect;
            DrawText(hdc, text, text.Length, ref focus, DT_CALCRECT | DT_SINGLELINE | DT_NOPREFIX);
            int offset = (textRect.bottom - textRect.top - (focus.bottom - focus.top)) / 2;
            focus.top += offset - 1;
            focus.bottom += offset + 1;
            focus.left -= 1;
            focus.right += 1;
            DrawFocusRect(hdc, focus);
        }
        SelectObject(hdc, oldFont);
        return CDRF_SKIPDEFAULT;
    }

    void Paint()
    {
        IntPtr hdc = BeginPaint(Handle, out var paint);

        // "Excluded apps" group frame
        IntPtr pen = CreatePen(PS_SOLID, 1, _palette.IsDark ? _palette.Border : Rgb(0xDC, 0xDC, 0xDC));
        IntPtr oldPen = SelectObject(hdc, pen);
        IntPtr oldFont = SelectObject(hdc, _font);
        const string groupTitle = "Excluded apps";
        GetTextExtentPoint32(hdc, groupTitle, groupTitle.Length, out var titleSize);
        var g = _excludedGroup;
        int frameTop = g.top + titleSize.cy / 2;
        int textX = g.left + Scale(8), gap = Scale(3);
        MoveToEx(hdc, textX - gap, frameTop, IntPtr.Zero);
        LineTo(hdc, g.left, frameTop);
        LineTo(hdc, g.left, g.bottom - 1);
        LineTo(hdc, g.right - 1, g.bottom - 1);
        LineTo(hdc, g.right - 1, frameTop);
        LineTo(hdc, textX + titleSize.cx + gap, frameTop);
        _ = SetBkMode(hdc, TRANSPARENT);
        _ = SetTextColor(hdc, _palette.Text);
        var titleRect = new RECT { left = textX, top = g.top, right = textX + titleSize.cx, bottom = g.top + titleSize.cy };
        DrawText(hdc, groupTitle, groupTitle.Length, ref titleRect, DT_SINGLELINE | DT_NOPREFIX);

        if (_palette.IsDark)
        {
            DrawFrame(hdc, _listFrame);
            DrawFrame(hdc, _excludedFrame);
        }

        SelectObject(hdc, oldFont);
        SelectObject(hdc, oldPen);
        DeleteObject(pen);
        EndPaint(Handle, paint);
    }

    static void DrawFrame(IntPtr hdc, RECT r)
    {
        MoveToEx(hdc, r.left, r.top, IntPtr.Zero);
        LineTo(hdc, r.right - 1, r.top);
        LineTo(hdc, r.right - 1, r.bottom - 1);
        LineTo(hdc, r.left, r.bottom - 1);
        LineTo(hdc, r.left, r.top);
    }

    void Layout()
    {
        // WM_SIZE can arrive while the constructor is still creating the controls.
        if (!_created) return;
        GetClientRect(Handle, out var client);
        int pad = Scale(12);
        int left = pad, right = client.right - pad;

        // Options, wrapping like a flow layout.
        int rowHeight = Scale(28);
        int x = left, y = pad;
        // windowHeight differs from height for a drop-down list, whose window includes its list.
        void Flow(IntPtr hwnd, int width, int height, int marginLeft, int marginRight, int windowHeight = 0)
        {
            if (x > left && x + marginLeft + width > right)
            {
                x = left;
                y += rowHeight;
            }
            Place(hwnd, x + marginLeft, y + (rowHeight - height) / 2, width, windowHeight > 0 ? windowHeight : height);
            x += marginLeft + width + marginRight;
        }
        foreach (var box in (ReadOnlySpan<IntPtr>)[_enabled, _leftAlt, _rightAlt, _startup])
            Flow(box, CheckBoxWidth(box), _textHeight + Scale(4), 0, Scale(20));
        Flow(_themeLabel, TextWidth(_themeLabel, _font), _textHeight, 0, 0);
        // A closed drop-down list sizes itself to its font; its window height is just the selection field.
        GetClientRect(_theme, out var combo);
        Flow(_theme, Scale(80), combo.bottom, Scale(4), Scale(20), windowHeight: Scale(200));
        if (Elevation.IsAdmin) Flow(_admin, TextWidth(_admin, _font), _textHeight, 0, 0);
        else Flow(_admin, ButtonWidth(_admin), Scale(26), 0, 0);
        int listTop = y + rowHeight + Scale(8);

        // From the bottom: buttons, then the excluded-apps group, then the list fills the rest.
        int buttonHeight = Scale(28);
        int buttonsTop = client.bottom - pad - buttonHeight;
        x = left;
        foreach (var button in _buttons)
        {
            int width = ButtonWidth(button);
            Place(button, x, buttonsTop, width, buttonHeight);
            x += width + Scale(6);
        }
        Place(_buttonsHint, x + Scale(6), buttonsTop + (buttonHeight - _textHeight) / 2, Math.Max(0, right - x - Scale(6)), _textHeight);

        int groupBottom = buttonsTop - Scale(10);
        int groupTop = groupBottom - Scale(130);
        _excludedGroup = new RECT { left = left, top = groupTop, right = right, bottom = groupBottom };
        int inner = Scale(9);
        int hintTop = groupTop + _textHeight + Scale(4);
        Place(_excludedHint, left + inner, hintTop, right - left - 2 * inner, _textHeight);
        _excludedFrame = new RECT { left = left + inner, top = hintTop + _textHeight + Scale(4), right = right - inner, bottom = groupBottom - inner };
        PlaceFramed(_excluded, _excludedFrame);

        _listFrame = new RECT { left = left, top = listTop, right = right, bottom = Math.Max(listTop, groupTop - Scale(10)) };
        PlaceFramed(_list.Handle, _listFrame);
        FitLastColumn();

        InvalidateRect(Handle, IntPtr.Zero, true);
    }

    /// <summary>In dark mode, leaves a pixel around the control for WM_PAINT to draw a frame in.</summary>
    void PlaceFramed(IntPtr hwnd, RECT r)
    {
        int inset = _palette.IsDark ? 1 : 0;
        Place(hwnd, r.left + inset, r.top + inset, r.right - r.left - 2 * inset, r.bottom - r.top - 2 * inset);
    }

    static void Place(IntPtr hwnd, int x, int y, int width, int height) =>
        SetWindowPos(hwnd, IntPtr.Zero, x, y, Math.Max(0, width), Math.Max(0, height), SWP_NOZORDER | SWP_NOACTIVATE);

    void FitLastColumn()
    {
        GetClientRect(_list.Handle, out var r);
        int available = r.right - _list.GetColumnWidth(0) - _list.GetColumnWidth(1);
        if (available > 100) _list.SetColumnWidth(2, available);
    }

    void CenterOnScreen()
    {
        var size = new RECT { right = Scale(900), bottom = Scale(760) };
        AdjustWindowRectExForDpi(ref size, Style, false, ExStyle, (uint)_dpi);
        int width = size.right - size.left, height = size.bottom - size.top;
        var monitor = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        GetMonitorInfo(MonitorFromWindow(Handle, MONITOR_DEFAULTTONEAREST), ref monitor);
        var work = monitor.rcWork;
        width = Math.Min(width, work.right - work.left);
        height = Math.Min(height, work.bottom - work.top);
        SetWindowPos(Handle, IntPtr.Zero, work.left + (work.right - work.left - width) / 2,
            work.top + (work.bottom - work.top - height) / 2, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    void OnDpiChanged(int dpi, RECT suggested)
    {
        int oldDpi = _dpi;
        _dpi = dpi;
        DeleteObject(_font);
        DeleteObject(_boldFont);
        CreateFonts();
        ApplyFonts();
        SendMessage(_tooltip, TTM_SETMAXTIPWIDTH, IntPtr.Zero, Scale(400));
        for (int i = 0; i < 2; i++) _list.SetColumnWidth(i, _list.GetColumnWidth(i) * dpi / oldDpi);

        _loading = true;
        _list.RecreateCheckBoxes();
        _loading = false;
        SyncToggles();
        if (_palette.IsDark) _list.UseDarkCheckBoxes(_dpi);

        SetWindowPos(Handle, IntPtr.Zero, suggested.left, suggested.top,
            suggested.right - suggested.left, suggested.bottom - suggested.top, SWP_NOZORDER | SWP_NOACTIVATE);
        Layout();
    }

    void ApplyTheme()
    {
        int dark = _palette.IsDark ? 1 : 0;
        DwmApi.DwmSetWindowAttribute(Handle, DwmApi.DWMWA_USE_IMMERSIVE_DARK_MODE, dark, sizeof(int));
        SendMessage(_tooltip, TTM_SETMAXTIPWIDTH, IntPtr.Zero, Scale(400));
        _list.SetColors(_palette.Control, _palette.Text);
        if (!_palette.IsDark) return;

        foreach (var hwnd in (ReadOnlySpan<IntPtr>)[.. _buttons, _admin, _excluded, _list.Handle, _tooltip])
        {
            UxTheme.AllowDarkModeForWindow(hwnd, true);
            UxTheme.SetWindowTheme(hwnd, "DarkMode_Explorer", null);
        }
        UxTheme.SetWindowTheme(_theme, "DarkMode_CFD", null);
        UxTheme.SetWindowTheme(_list.Header, "DarkMode_ItemsView", null);
        _list.UseHeaderTextColor(_palette.Text);
        // After SetWindowTheme, which makes the list rebuild its checkbox images.
        _list.UseDarkCheckBoxes(_dpi);
    }

    void LoadFromSettings()
    {
        _loading = true;
        SendMessage(_startup, BM_SETCHECK, StartWithWindows.IsEnabled ? BST_CHECKED : 0, IntPtr.Zero);
        SendMessage(_theme, CB_SETCURSEL, (int)_settings.Theme, IntPtr.Zero);
        SetWindowText(_excluded, string.Join("\r\n", _settings.ExcludedApps));
        _loading = false;
        SyncToggles();
    }

    /// <summary>Settings can also change from the tray menu or the buttons below the list.</summary>
    void OnSettingsChanged(object? sender, EventArgs e)
    {
        // A handler that ran before this one may have destroyed the window (e.g. to apply a theme).
        if (Handle != IntPtr.Zero) SyncToggles();
    }

    /// <summary>
    /// Updates the checkboxes. The excluded-apps text is left alone so typing in it isn't disturbed,
    /// and the theme can only change here.
    /// </summary>
    void SyncToggles()
    {
        _loading = true;
        SetChecked(_enabled, _settings.Enabled);
        SetChecked(_leftAlt, _settings.UseLeftAlt);
        SetChecked(_rightAlt, _settings.UseRightAlt);
        var all = ShortcutCatalog.All;
        for (int i = 0; i < all.Count; i++) _list.SetChecked(i, _settings.IsEnabled(all[i]));
        _loading = false;
    }

    void SaveExcluded()
    {
        KillTimer(Handle, ExcludedSaveTimer);
        _settings.SetExcludedApps(GetText(_excluded)
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList());
    }

    void SetStartup(bool enabled)
    {
        try
        {
            StartWithWindows.Set(enabled);
        }
        catch (Exception ex)
        {
            MessageBox(Handle, $"Couldn't update the startup setting:\n{ex.Message}", Title, MB_OK | MB_ICONWARNING);
            _loading = true;
            SetChecked(_startup, StartWithWindows.IsEnabled);
            _loading = false;
        }
    }

    // Controls

    IntPtr Child(string className, string? text, uint style, uint exStyle = 0) =>
        CreateWindowEx(exStyle, className, text, WS_CHILD | WS_VISIBLE | style, 0, 0, 0, 0, Handle, IntPtr.Zero, Instance, IntPtr.Zero);

    IntPtr CheckBox(string text) => Child("BUTTON", text, BS_AUTOCHECKBOX | WS_TABSTOP);

    IntPtr Label(string text)
    {
        const uint SS_NOPREFIX = 0x80;
        return Child("STATIC", text, SS_NOPREFIX);
    }

    bool IsCheckBox(IntPtr hwnd) => hwnd == _enabled || hwnd == _leftAlt || hwnd == _rightAlt || hwnd == _startup;

    static bool IsChecked(IntPtr hwnd) => SendMessage(hwnd, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == BST_CHECKED;

    static void SetChecked(IntPtr hwnd, bool value) => SendMessage(hwnd, BM_SETCHECK, value ? BST_CHECKED : 0, IntPtr.Zero);

    void AddTooltip(IntPtr control, string text)
    {
        fixed (char* p = text)
        {
            var tool = new TOOLINFOW
            {
                cbSize = (uint)sizeof(TOOLINFOW),
                uFlags = TTF_IDISHWND | TTF_SUBCLASS,
                hwnd = Handle,
                uId = (nuint)control,
                lpszText = (IntPtr)p,
            };
            SendMessage(_tooltip, TTM_ADDTOOLW, IntPtr.Zero, (IntPtr)(&tool));
        }
    }

    static string GetText(IntPtr hwnd)
    {
        int length = GetWindowTextLength(hwnd);
        if (length == 0) return "";
        var buffer = new char[length + 1];
        fixed (char* p = buffer)
        {
            int copied = GetWindowText(hwnd, p, buffer.Length);
            return new string(p, 0, copied);
        }
    }

    // Fonts and measuring

    void CreateFonts()
    {
        int height = -(9 * _dpi + 36) / 72; // 9 pt
        const uint DEFAULT_CHARSET = 1, CLEARTYPE_QUALITY = 5;
        _font = CreateFont(height, 0, 0, 0, FW_NORMAL, 0, 0, 0, DEFAULT_CHARSET, 0, 0, CLEARTYPE_QUALITY, 0, "Segoe UI");
        _boldFont = CreateFont(height, 0, 0, 0, FW_BOLD, 0, 0, 0, DEFAULT_CHARSET, 0, 0, CLEARTYPE_QUALITY, 0, "Segoe UI");
        _textHeight = Measure("Ag", _font).cy;
    }

    void ApplyFonts()
    {
        foreach (var hwnd in (ReadOnlySpan<IntPtr>)[_leftAlt, _rightAlt, _startup, _themeLabel, _theme, _admin,
                     _list.Handle, _excludedHint, _excluded, .. _buttons, _buttonsHint, _tooltip])
            SendMessage(hwnd, WM_SETFONT, _font, 1);
        SendMessage(_enabled, WM_SETFONT, _boldFont, 1);
    }

    SIZE Measure(string text, IntPtr font)
    {
        IntPtr hdc = GetDC(Handle);
        IntPtr old = SelectObject(hdc, font);
        GetTextExtentPoint32(hdc, text, text.Length, out var size);
        SelectObject(hdc, old);
        _ = ReleaseDC(Handle, hdc);
        return size;
    }

    int TextWidth(IntPtr hwnd, IntPtr font) => Measure(GetText(hwnd), font).cx;

    int CheckBoxWidth(IntPtr hwnd) => Scale(22) + TextWidth(hwnd, hwnd == _enabled ? _boldFont : _font);

    int ButtonWidth(IntPtr hwnd) => TextWidth(hwnd, _font) + Scale(24);

    int Scale(int value) => value * _dpi / 96;
}
