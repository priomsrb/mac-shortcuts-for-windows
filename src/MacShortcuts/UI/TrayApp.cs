using System.Runtime.InteropServices;
using MacShortcuts.Interop;
using MacShortcuts.Platform;
using MacShortcuts.Remapping;
using MacShortcuts.Settings;
using static MacShortcuts.Interop.Shell32;
using static MacShortcuts.Interop.User32;
using static MacShortcuts.Interop.WtsApi32;

namespace MacShortcuts.UI;

/// <summary>
/// Owns the tray icon, the settings window and the remapper for the app's lifetime, through a
/// hidden top-level window (it must be top-level to receive broadcasts like WM_SETTINGCHANGE).
/// </summary>
internal sealed unsafe class TrayApp : Window, IDisposable
{
    const int WM_TRAY = WM_APP + 1;
    const int WM_SHOW_SETTINGS = WM_APP + 2;
    const int WM_QUERYENDSESSION = 0x0011;
    const int WM_ENDSESSION = 0x0016;
    const int MenuSettings = 1, MenuEnabled = 2, MenuRestartAsAdmin = 3, MenuExit = 4;

    static readonly uint TaskbarCreated = RegisterWindowMessage("TaskbarCreated");

    readonly SettingsController _settings;
    readonly KeyRemapper _remapper = new();
    readonly TrayIcon _tray;
    MainWindow? _window;
    bool _trayHintShown;
    bool _dark;
    bool _exited;

    public TrayApp(bool startMinimized)
    {
        _settings = new SettingsController(AppSettings.Load(), SaveSettings);
        Theming.Apply(_settings.Theme);
        _dark = Theming.IsDark(_settings.Theme);

        CreateHandle("MacShortcuts.Tray", "Mac Shortcuts", WS_POPUP, WS_EX_TOOLWINDOW);
        WTSRegisterSessionNotification(Handle, NOTIFY_FOR_THIS_SESSION);
        _tray = new TrayIcon(Handle, WM_TRAY);
        UpdateTrayState();

        _remapper.Start();
        _remapper.Update(_settings.ToConfig());
        _settings.Changed += OnSettingsChanged;

        if (!startMinimized) ShowSettings();
    }

    /// <summary>Runs the message loop until the user exits.</summary>
    public void Run()
    {
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (_window is { Handle: var dialog } && dialog != IntPtr.Zero)
            {
                if (msg.message is WM_LBUTTONDOWN or WM_RBUTTONDOWN && (msg.hwnd == dialog || IsChild(dialog, msg.hwnd)))
                    _window.HideFocusRings();
                // Tab and arrow-key navigation between the settings window's controls.
                if (IsDialogMessage(dialog, msg)) continue;
            }
            TranslateMessage(msg);
            DispatchMessage(msg);
        }
    }

    /// <summary>Shows the settings window. Can be called from any thread.</summary>
    public void RequestShowSettings() => PostMessage(Handle, WM_SHOW_SETTINGS, IntPtr.Zero, IntPtr.Zero);

    public void ShowSettings()
    {
        _window ??= CreateWindow(placement: null);
        _window.Show();
    }

    protected override IntPtr WndProc(uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_TRAY:
                switch (LoWord(lParam))
                {
                    case NIN_SELECT or NIN_KEYSELECT:
                        ShowSettings();
                        break;
                    case WM_CONTEXTMENU:
                        ShowMenu(LoWord(wParam), HiWord(wParam));
                        break;
                }
                return 0;

            case WM_SHOW_SETTINGS:
                ShowSettings();
                return 0;

            case WM_WTSSESSION_CHANGE:
                // Key-ups are missed while the session is locked or switched away.
                _remapper.ResetKeyState();
                return 0;

            case WM_SETTINGCHANGE:
                // Follow Windows switching between light and dark app mode.
                if (lParam != IntPtr.Zero && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet"
                    && _settings.Theme == AppTheme.System && Theming.SystemIsDark != _dark)
                    ApplyTheme();
                return 0;

            case WM_QUERYENDSESSION:
                return 1;

            case WM_ENDSESSION:
                // Windows is signing out: save any pending edit before the process is ended.
                if (wParam != IntPtr.Zero) Exit();
                return 0;
        }

        if (msg == TaskbarCreated)
        {
            // Explorer restarted, taking the tray icon with it.
            _tray.Recreate();
            return 0;
        }
        return base.WndProc(msg, wParam, lParam);
    }

    void OnSettingsChanged(object? sender, EventArgs e)
    {
        _remapper.Update(_settings.ToConfig());
        UpdateTrayState();
        if (Theming.IsDark(_settings.Theme) != _dark) ApplyTheme();
    }

    void UpdateTrayState()
    {
        bool enabled = _settings.Enabled;
        _tray.Update(enabled ? AppIcons.Small : AppIcons.SmallDisabled,
            enabled ? "Mac Shortcuts (on)" : "Mac Shortcuts (off)");
    }

    /// <summary>Controls take their theme when created, so recreate the settings window.</summary>
    void ApplyTheme()
    {
        _dark = Theming.IsDark(_settings.Theme);
        Theming.Apply(_settings.Theme);

        if (_window == null) return;
        bool visible = _window.IsVisible;
        var placement = _window.Placement;
        var page = _window.CurrentPage;
        _window.Destroy();
        _window = null;
        if (!visible) return;

        _window = CreateWindow(placement, page);
        _window.Show();
    }

    void ShowMenu(int x, int y)
    {
        IntPtr menu = CreatePopupMenu();
        // The default item (bold) is what clicking the icon does.
        AppendMenu(menu, MF_STRING, MenuSettings, "Settings");
        SetMenuDefaultItem(menu, MenuSettings, 0);
        AppendMenu(menu, MF_SEPARATOR, 0, null);
        AppendMenu(menu, MF_STRING | (_settings.Enabled ? MF_CHECKED : 0), MenuEnabled, "Enabled");
        IntPtr shield = IntPtr.Zero;
        if (!Elevation.IsAdmin)
        {
            AppendMenu(menu, MF_STRING, MenuRestartAsAdmin, "Restart as administrator");
            shield = AppIcons.CreateShieldBitmap(GetDpiForWindow(Handle));
            if (shield != IntPtr.Zero)
                SetMenuItemInfo(menu, MenuRestartAsAdmin, false, new MENUITEMINFOW { cbSize = (uint)sizeof(MENUITEMINFOW), fMask = MIIM_BITMAP, hbmpItem = shield });
        }
        AppendMenu(menu, MF_SEPARATOR, 0, null);
        AppendMenu(menu, MF_STRING, MenuExit, "Exit");

        // Without this the menu doesn't close when the user clicks elsewhere.
        SetForegroundWindow(Handle);
        int command = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON, x, y, Handle, IntPtr.Zero);
        PostMessage(Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(menu);
        if (shield != IntPtr.Zero) Gdi32.DeleteObject(shield);

        switch (command)
        {
            case MenuSettings: ShowSettings(); break;
            case MenuEnabled: _settings.SetEnabled(!_settings.Enabled); break;
            case MenuRestartAsAdmin: RestartAsAdmin(); break;
            case MenuExit: Exit(); break;
        }
    }

    MainWindow CreateWindow(WINDOWPLACEMENT? placement, MainWindow.Page page = MainWindow.Page.Shortcuts)
    {
        var window = new MainWindow(_settings, _remapper.SetCapture, _dark, placement, page);
        window.HiddenToTray += (_, _) => ShowTrayHint();
        window.RestartAsAdminRequested += (_, _) => RestartAsAdmin();
        return window;
    }

    void RestartAsAdmin()
    {
        if (Elevation.TryRestartElevated()) Exit();
    }

    void SaveSettings(AppSettings settings)
    {
        try
        {
            settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The change still applies until the app exits.
            _tray.ShowBalloon("Couldn't save settings", ex.Message, warning: true);
        }
    }

    void ShowTrayHint()
    {
        if (_trayHintShown) return;
        _trayHintShown = true;
        _tray.ShowBalloon("Mac Shortcuts is still running",
            "It keeps working from the system tray. Right-click the tray icon to exit.", warning: false);
    }

    public void Dispose() => Exit();

    void Exit()
    {
        if (_exited) return;
        _exited = true;
        _settings.Changed -= OnSettingsChanged;
        // Destroying the window saves any pending excluded-apps edit.
        _window?.Destroy();
        _window = null;
        _remapper.Dispose();
        _tray.Remove();
        WTSUnRegisterSessionNotification(Handle);
        DestroyWindow(Handle);
        PostQuitMessage(0);
    }
}
