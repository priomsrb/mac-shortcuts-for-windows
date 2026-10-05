using MacShortcuts.Interop;
using MacShortcuts.Settings;
using MacShortcuts.UI.Drawing;
using static MacShortcuts.Interop.ComCtl32;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI;

/// <summary>
/// The settings window: an icon rail on the left switches between the Shortcuts, Excluded apps and
/// Settings pages. Closing it only hides it; the app keeps running in the tray.
/// </summary>
/// <remarks>
/// The controls are native (so keyboard navigation and screen readers work as usual) but draw
/// themselves in the window's palette through NM_CUSTOMDRAW and WM_DRAWITEM. Everything else
/// (headings, panels, descriptions) is painted by the window from <see cref="_art"/>, which
/// <see cref="Layout"/> rebuilds for the current page.
/// </remarks>
internal sealed unsafe partial class MainWindow : Window
{
    const string Title = "Mac Shortcuts for Windows";
    const uint Style = WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN;
    const uint ExStyle = WS_EX_CONTROLPARENT;
    const int WM_SET_THEME = WM_APP + 1;
    const int WM_REDRAW_CHECKBOXES = WM_APP + 2;

    internal enum Page { Shortcuts, Excluded, Settings }

    /// <summary>How a button draws itself.</summary>
    enum ButtonKind { Normal, Primary, Link, CheckBox, Toggle, Nav }

    /// <summary>What's behind a control, which shows around its rounded corners.</summary>
    enum Backdrop { Window, Surface, SurfaceAlt, Rail }

    readonly record struct ButtonStyle(ButtonKind Kind, Backdrop Backdrop);

    readonly SettingsController _settings;
    readonly Palette _palette;
    readonly IntPtr _surfaceBrush;
    readonly IntPtr _windowBrush;
    Fonts _fonts;
    int _dpi;
    bool _loading;
    bool _created;
    Page _page;

    readonly Dictionary<IntPtr, ButtonStyle> _buttons = [];
    readonly List<IntPtr>[] _pageControls = [[], [], []];
    readonly List<Action<IntPtr>> _art = [];

    readonly IntPtr[] _nav;
    readonly IntPtr _masterToggle;

    /// <summary>The user closed the window; the app keeps running in the tray.</summary>
    public event EventHandler? HiddenToTray;

    public event EventHandler? RestartAsAdminRequested;

    /// <param name="placement">Where to show the window (e.g. to replace one with the old theme), or null to centre it.</param>
    /// <param name="page">The page to show first.</param>
    public MainWindow(SettingsController settings, bool dark, WINDOWPLACEMENT? placement, Page page = Page.Shortcuts)
    {
        _settings = settings;
        _palette = Palette.For(dark);
        _surfaceBrush = CreateSolidBrush(_palette.Surface);
        _windowBrush = CreateSolidBrush(_palette.Window);

        CreateHandle("MacShortcuts.Settings", Title, Style, ExStyle);
        _dpi = (int)GetDpiForWindow(Handle);
        _fonts = new Fonts(_dpi);
        SendMessage(Handle, WM_SETICON, ICON_BIG, AppIcons.Large);
        SendMessage(Handle, WM_SETICON, ICON_SMALL, AppIcons.Small);

        // Children are created in tab order.
        _nav =
        [
            Button("Shortcuts", ButtonKind.Nav, Backdrop.Rail),
            Button("Excluded", ButtonKind.Nav, Backdrop.Rail),
            Button("Settings", ButtonKind.Nav, Backdrop.Rail),
        ];
        _masterToggle = Button("Remapping", ButtonKind.Toggle, Backdrop.Rail);

        CreateShortcutsPage();
        CreateExcludedPage();
        CreateSettingsPage();

        // Themes first: setting a list's theme rebuilds its checkbox images, which ApplyFonts redraws.
        ApplyTheme();
        ApplyFonts();
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
        ShowPage(page);
    }

    public Page CurrentPage => _page;

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
        if (GetFocus() is var focus && (focus == IntPtr.Zero || !IsChild(Handle, focus))) SetFocus(_nav[(int)_page]);
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
                ShowWindow(Handle, SW_HIDE);
                HiddenToTray?.Invoke(this, EventArgs.Empty);
                return 0;

            case WM_DESTROY:
                // The window is destroyed without closing when the theme changes or the app exits.
                _settings.Changed -= OnSettingsChanged;
                break;

            case WM_SIZE:
                Layout();
                return 0;

            case WM_GETMINMAXINFO:
                var min = new RECT { right = Scale(880), bottom = Scale(660) };
                AdjustWindowRectExForDpi(ref min, Style, false, ExStyle, (uint)_dpi);
                ((MINMAXINFO*)lParam)->ptMinTrackSize = new POINT { X = min.right - min.left, Y = min.bottom - min.top };
                return 0;

            case WM_DPICHANGED:
                OnDpiChanged(LoWord(wParam), *(RECT*)lParam);
                return 0;

            case WM_ERASEBKGND:
                // WM_PAINT paints everything, double-buffered.
                return 1;

            case WM_PAINT:
                PaintWindow();
                return 0;

            case WM_CTLCOLOREDIT:
            case WM_CTLCOLORSTATIC:
                _ = SetTextColor(wParam, _palette.Text);
                _ = SetBkColor(wParam, _palette.Surface);
                return _surfaceBrush;

            case WM_CTLCOLORLISTBOX:
                // The category list sits on the window; a drop-down list on a panel.
                bool onWindow = lParam == _categories;
                _ = SetTextColor(wParam, _palette.Text);
                _ = SetBkColor(wParam, onWindow ? _palette.Window : _palette.Surface);
                return onWindow ? _windowBrush : _surfaceBrush;

            case WM_MEASUREITEM:
                ((MEASUREITEMSTRUCT*)lParam)->itemHeight = (uint)CategoryItemHeight;
                return 1;

            case WM_DRAWITEM:
                var item = (DRAWITEMSTRUCT*)lParam;
                if (item->hwndItem == _categories) DrawCategory(item);
                return 1;

            case WM_COMMAND:
                OnCommand(wParam, lParam);
                return 0;

            case WM_NOTIFY:
                return OnNotify((NMHDR*)lParam);

            case WM_SET_THEME:
                _settings.SetTheme((AppTheme)wParam.ToInt32());
                return 0;

            case WM_UPDATEUISTATE:
                // Focus rings were shown or hidden; the owner-drawn category list doesn't repaint for that itself.
                InvalidateRect(_categories, IntPtr.Zero, false);
                break;

            case WM_THEMECHANGED:
                // The list rebuilds its checkbox images too, so redraw them after it.
                PostMessage(Handle, WM_REDRAW_CHECKBOXES, IntPtr.Zero, IntPtr.Zero);
                break;

            case WM_REDRAW_CHECKBOXES:
                _shortcutList.DrawCheckBoxes();
                return 0;
        }
        return base.WndProc(msg, wParam, lParam);
    }

    protected override void OnHandleDestroyed()
    {
        _fonts.Delete();
        DeleteObject(_surfaceBrush);
        DeleteObject(_windowBrush);
    }

    void OnCommand(IntPtr wParam, IntPtr control)
    {
        int code = HiWord(wParam);
        if (control == IntPtr.Zero)
        {
            // Enter and Esc, from IsDialogMessage.
            int id = LoWord(wParam);
            if (id == IDOK && GetFocus() == _addEdit) AddTypedApp();
            else if (id == IDCANCEL) PostMessage(Handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return;
        }
        if (_loading) return;

        if (OnShortcutsCommand(control, code) || OnExcludedCommand(control, code) || OnSettingsCommand(control, code)) return;
        if (code != BN_CLICKED) return;

        int nav = Array.IndexOf(_nav, control);
        if (nav >= 0) ShowPage((Page)nav);
        else if (control == _masterToggle) _settings.SetEnabled(IsChecked(_masterToggle));
    }

    IntPtr OnNotify(NMHDR* header)
    {
        if (header->code == NM_CUSTOMDRAW && _buttons.TryGetValue(header->hwndFrom, out var style))
            return DrawButton((NMCUSTOMDRAW*)header, style);
        if (header->hwndFrom == _shortcutList.Handle) return OnShortcutListNotify(header);
        if (header->hwndFrom == _excludedList.Handle) return OnExcludedListNotify(header);
        return 0;
    }

    void ShowPage(Page page)
    {
        _page = page;
        for (int i = 0; i < _pageControls.Length; i++)
        {
            foreach (var hwnd in _pageControls[i]) ShowWindow(hwnd, i == (int)page ? SW_SHOW : SW_HIDE);
        }
        if (page == Page.Excluded) OnExcludedPageShown();
        foreach (var hwnd in _nav) InvalidateRect(hwnd, IntPtr.Zero, false);
        Layout();
    }

    // Painting

    void PaintWindow()
    {
        IntPtr hdc = BeginPaint(Handle, out var paint);
        GetClientRect(Handle, out var client);
        IntPtr buffer = CreateCompatibleDC(hdc);
        IntPtr bitmap = CreateCompatibleBitmap(hdc, client.right, client.bottom);
        IntPtr old = SelectObject(buffer, bitmap);

        Paint.Fill(buffer, client, _palette.Window);
        foreach (var draw in _art) draw(buffer);
        BitBlt(hdc, 0, 0, client.right, client.bottom, buffer, 0, 0, SRCCOPY);

        SelectObject(buffer, old);
        DeleteObject(bitmap);
        DeleteDC(buffer);
        EndPaint(Handle, paint);
    }

    IntPtr DrawButton(NMCUSTOMDRAW* cd, ButtonStyle style)
    {
        if (cd->dwDrawStage != CDDS_PREPAINT) return CDRF_DODEFAULT;

        IntPtr hwnd = cd->hdr.hwndFrom, hdc = cd->hdc;
        var rc = cd->rc;
        int width = rc.right - rc.left, height = rc.bottom - rc.top;
        bool hot = (cd->uItemState & CDIS_HOT) != 0;
        bool pressed = (cd->uItemState & CDIS_SELECTED) != 0;
        bool disabled = (cd->uItemState & CDIS_DISABLED) != 0;
        bool focused = (cd->uItemState & CDIS_FOCUS) != 0 && (cd->uItemState & CDIS_SHOWKEYBOARDCUES) != 0;
        uint backdrop = style.Backdrop switch
        {
            Backdrop.Surface => _palette.Surface,
            Backdrop.SurfaceAlt => _palette.SurfaceAlt,
            Backdrop.Rail => _palette.Rail,
            _ => _palette.Window,
        };
        string text = GetText(hwnd);
        RECT focusRect = rc;
        int focusRadius = Scale(6);

        switch (style.Kind)
        {
            case ButtonKind.Normal:
            case ButtonKind.Primary:
                bool primary = style.Kind == ButtonKind.Primary;
                uint fill = primary ? _palette.Accent : pressed ? _palette.Divider : hot ? _palette.ControlHover : _palette.Surface;
                uint border = primary ? _palette.Accent : _palette.ControlBorder;
                Canvas.Render(hdc, rc, backdrop, c => c.RoundRect(0, 0, width, height, Scale(6), fill, border));
                Paint.Text(hdc, text, primary ? _fonts.BodyStrong : _fonts.Body,
                    disabled ? _palette.MutedText : primary ? _palette.OnAccent : _palette.Text, rc,
                    DT_SINGLELINE | DT_VCENTER | DT_CENTER);
                break;

            case ButtonKind.Link:
                Paint.Fill(hdc, rc, backdrop);
                Paint.Text(hdc, text, _fonts.Small, _palette.Accent, rc);
                if (hot)
                {
                    var size = Paint.Measure(text, _fonts.Small);
                    int y = rc.top + (height + size.cy) / 2 - Scale(1);
                    Paint.HorizontalLine(hdc, rc.left, rc.left + size.cx, y, _palette.Accent);
                }
                focusRadius = Scale(4);
                break;

            case ButtonKind.CheckBox:
                bool isChecked = IsChecked(hwnd);
                int box = Scale(16);
                Paint.Fill(hdc, rc, backdrop);
                Canvas.Render(hdc, Paint.Rect(rc.left, rc.top + (height - box) / 2, box, box), backdrop,
                    c => Glyphs.CheckBox(c, 0, 0, box, isChecked, _palette));
                var label = rc with { left = rc.left + box + Scale(12) };
                Paint.Text(hdc, text, _fonts.Label, _palette.Text, label);
                focusRect = label with { left = label.left - Scale(6), right = label.left + Paint.Measure(text, _fonts.Label).cx + Scale(6) };
                focusRadius = Scale(4);
                break;

            case ButtonKind.Toggle:
                bool on = IsChecked(hwnd);
                int switchHeight = Scale(20);
                Canvas.Render(hdc, rc with { bottom = rc.top + Scale(6) + switchHeight + Scale(2) }, backdrop,
                    c => Glyphs.Toggle(c, (width - 2 * switchHeight) / 2f, Scale(6), switchHeight, on, _palette));
                var caption = rc with { top = rc.top + Scale(6) + switchHeight + Scale(2) };
                Paint.Fill(hdc, caption, backdrop);
                Paint.Text(hdc, on ? "On" : "Off", _fonts.CaptionStrong, on ? _palette.RailSelectedText : _palette.MutedText,
                    caption, DT_SINGLELINE | DT_VCENTER | DT_CENTER);
                break;

            case ButtonKind.Nav:
                bool selected = Array.IndexOf(_nav, hwnd) == (int)_page;
                var icon = Array.IndexOf(_nav, hwnd) switch { 0 => Icon.Keyboard, 1 => Icon.Block, _ => Icon.Wrench };
                uint color = selected ? _palette.RailSelectedText : _palette.MutedText;
                int iconSize = Scale(20);
                Canvas.Render(hdc, rc, backdrop, c =>
                {
                    if (selected) c.RoundRect(0, 0, width, height, Scale(8), _palette.RailSelected, _palette.RailSelectedBorder);
                    else if (hot) c.FillRoundRect(0, 0, width, height, Scale(8), _palette.RailHover);
                    Glyphs.Draw(c, icon, (width - iconSize) / 2f, Scale(9), iconSize, color);
                });
                var labelRect = rc with { top = rc.top + Scale(9) + iconSize + Scale(4), bottom = rc.bottom - Scale(6) };
                Paint.Text(hdc, text, selected ? _fonts.CaptionStrong : _fonts.Caption, color, labelRect,
                    DT_SINGLELINE | DT_VCENTER | DT_CENTER);
                focusRadius = Scale(8);
                break;
        }

        if (focused) DrawFocusRing(hdc, focusRect, focusRadius);
        return CDRF_SKIPDEFAULT;
    }

    /// <summary>Outlines a control with keyboard focus, just inside <paramref name="rc"/>.</summary>
    void DrawFocusRing(IntPtr hdc, RECT rc, int radius)
    {
        float line = Math.Max(2, Scale(2));
        int width = rc.right - rc.left, height = rc.bottom - rc.top;
        Canvas.Render(hdc, rc, null, c => c.StrokeRoundRect(line / 2, line / 2, width - line, height - line, radius - line / 2, line, _palette.Accent));
    }

    /// <summary>
    /// Hides focus rings until the keyboard is next used to move around (IsDialogMessage shows them
    /// again), so clicking a control doesn't outline it.
    /// </summary>
    public void HideFocusRings()
    {
        const int UIS_SET = 1, UISF_HIDEFOCUS = 0x1;
        SendMessage(Handle, WM_CHANGEUISTATE, (UISF_HIDEFOCUS << 16) | UIS_SET, IntPtr.Zero);
    }

    // Layout

    int RailWidth => Scale(76);

    void Layout()
    {
        // WM_SIZE can arrive while the constructor is still creating the controls.
        if (!_created) return;
        GetClientRect(Handle, out var client);
        _art.Clear();
        LayoutRail(client);
        switch (_page)
        {
            case Page.Shortcuts: LayoutShortcutsPage(client); break;
            case Page.Excluded: LayoutExcludedPage(client); break;
            case Page.Settings: LayoutSettingsPage(client); break;
        }
        InvalidateRect(Handle, IntPtr.Zero, false);
    }

    void LayoutRail(RECT client)
    {
        var rail = client with { right = RailWidth };
        int iconSize = Scale(32);
        var icon = Paint.Rect((RailWidth - iconSize) / 2, Scale(14), iconSize, iconSize);
        _art.Add(hdc =>
        {
            Paint.Fill(hdc, rail, _palette.Rail);
            Paint.VerticalLine(hdc, rail.right - 1, rail.top, rail.bottom, _palette.RailBorder);
            DrawIconEx(hdc, icon.left, icon.top, AppIcons.Large, iconSize, iconSize, 0, IntPtr.Zero, DI_NORMAL);
        });

        int x = Scale(8), width = RailWidth - 2 * Scale(8), y = icon.bottom + Scale(18);
        foreach (var nav in _nav)
        {
            Place(nav, Paint.Rect(x, y, width, Scale(58)));
            y += Scale(58) + Scale(6);
        }
        Place(_masterToggle, Paint.Rect(x, client.bottom - Scale(14) - Scale(48), width, Scale(48)));
    }

    static void Place(IntPtr hwnd, RECT r) =>
        SetWindowPos(hwnd, IntPtr.Zero, r.left, r.top, Math.Max(0, r.right - r.left), Math.Max(0, r.bottom - r.top), SWP_NOZORDER | SWP_NOACTIVATE);

    void CenterOnScreen()
    {
        var size = new RECT { right = Scale(1040), bottom = Scale(740) };
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
        _dpi = dpi;
        _fonts.Delete();
        _fonts = new Fonts(_dpi);
        ApplyFonts();
        OnShortcutsDpiChanged();
        OnExcludedDpiChanged();
        SetWindowPos(Handle, IntPtr.Zero, suggested.left, suggested.top,
            suggested.right - suggested.left, suggested.bottom - suggested.top, SWP_NOZORDER | SWP_NOACTIVATE);
        Layout();
    }

    void ApplyFonts()
    {
        foreach (var hwnd in (ReadOnlySpan<IntPtr>)[_filter, _addEdit, _theme])
            SendMessage(hwnd, WM_SETFONT, _fonts.Body, 1);
        _shortcutList.CheckBoxPadding = Scale(10);
        _shortcutList.ApplyStyle(_palette, _fonts.Body, _fonts.SmallStrong, Scale(34), _dpi, headerPadding: Scale(6), groupGap: Scale(16));
        _excludedList.ApplyStyle(_palette, _fonts.Body, _fonts.SmallStrong, Scale(56), _dpi);
        SendMessage(_categories, LB_SETITEMHEIGHT, IntPtr.Zero, CategoryItemHeight);
    }

    void ApplyTheme()
    {
        int dark = _palette.IsDark ? 1 : 0;
        DwmApi.DwmSetWindowAttribute(Handle, DwmApi.DWMWA_USE_IMMERSIVE_DARK_MODE, dark, sizeof(int));
        // The Explorer themes give the lists modern scrollbars; the dark one darkens them.
        string lists = _palette.IsDark ? "DarkMode_Explorer" : "Explorer";
        foreach (var hwnd in (ReadOnlySpan<IntPtr>)[_shortcutList.Handle, _excludedList.Handle, _categories])
        {
            UxTheme.AllowDarkModeForWindow(hwnd, _palette.IsDark);
            UxTheme.SetWindowTheme(hwnd, lists, null);
        }
        if (_palette.IsDark) UxTheme.SetWindowTheme(_theme, "DarkMode_CFD", null);
    }

    void LoadFromSettings()
    {
        _loading = true;
        LoadSettingsPage();
        _loading = false;
        SyncToggles();
        SyncExcluded();
    }

    /// <summary>Settings can also change from the tray menu.</summary>
    void OnSettingsChanged(object? sender, EventArgs e)
    {
        // A handler that ran before this one may have destroyed the window (e.g. to apply a theme).
        if (Handle == IntPtr.Zero) return;
        SyncToggles();
        SyncExcluded();
    }

    /// <summary>Updates every checkbox and toggle from the settings.</summary>
    void SyncToggles()
    {
        _loading = true;
        SetChecked(_masterToggle, _settings.Enabled);
        InvalidateRect(_masterToggle, IntPtr.Zero, false);
        SyncSettingsPage();
        SyncShortcutList();
        _loading = false;
        Layout();
    }

    // Controls

    IntPtr Child(Page? page, string className, string? text, uint style, uint exStyle = 0)
    {
        IntPtr hwnd = CreateWindowEx(exStyle, className, text, WS_CHILD | style, 0, 0, 0, 0, Handle, IntPtr.Zero, Instance, IntPtr.Zero);
        if (page is { } p) _pageControls[(int)p].Add(hwnd);
        else ShowWindow(hwnd, SW_SHOW);
        return hwnd;
    }

    IntPtr Button(string text, ButtonKind kind, Backdrop backdrop, Page? page = null)
    {
        uint style = kind is ButtonKind.CheckBox or ButtonKind.Toggle ? BS_AUTOCHECKBOX : BS_PUSHBUTTON;
        IntPtr hwnd = Child(page, "BUTTON", text, style | WS_TABSTOP);
        _buttons[hwnd] = new ButtonStyle(kind, backdrop);
        return hwnd;
    }

    IntPtr Edit(Page page, string cue)
    {
        IntPtr hwnd = Child(page, "EDIT", null, ES_AUTOHSCROLL | WS_TABSTOP);
        // Shown even while the box has focus.
        SendMessage(hwnd, EM_SETCUEBANNER, 1, cue);
        return hwnd;
    }

    /// <summary>Paints a single-line text box's rounded frame, with an optional icon, and centres the edit inside it.</summary>
    void PlaceTextBox(IntPtr edit, RECT frame, uint backdrop, Icon? icon = null)
    {
        int radius = Scale(6), iconSize = Scale(14);
        int left = frame.left + Scale(10) + (icon == null ? 0 : iconSize + Scale(6));
        int height = Paint.Measure("Ag", _fonts.Body).cy;
        Place(edit, Paint.Rect(left, frame.top + (frame.bottom - frame.top - height) / 2, frame.right - Scale(8) - left, height));
        _art.Add(hdc =>
        {
            int width = frame.right - frame.left, frameHeight = frame.bottom - frame.top;
            Canvas.Render(hdc, frame, backdrop, c =>
            {
                c.RoundRect(0, 0, width, frameHeight, radius, _palette.Surface, _palette.ControlBorder);
                if (icon is { } i) Glyphs.Draw(c, i, Scale(10), (frameHeight - iconSize) / 2f, iconSize, _palette.MutedText);
            });
        });
    }

    int ButtonWidth(IntPtr hwnd) => Paint.Measure(GetText(hwnd), _fonts.BodyStrong).cx + Scale(28);

    static bool IsChecked(IntPtr hwnd) => SendMessage(hwnd, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == BST_CHECKED;

    static void SetChecked(IntPtr hwnd, bool value) => SendMessage(hwnd, BM_SETCHECK, value ? BST_CHECKED : 0, IntPtr.Zero);

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

    int Scale(int value) => value * _dpi / 96;
}
