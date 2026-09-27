using MacShortcuts.Platform;
using MacShortcuts.Remapping;
using MacShortcuts.Settings;
using Microsoft.Win32;

namespace MacShortcuts.UI;

/// <summary>Owns the tray icon, the settings window and the remapper for the app's lifetime.</summary>
internal sealed class TrayApp : ApplicationContext
{
    readonly SettingsController _settings;
    readonly KeyRemapper _remapper = new();
    readonly NotifyIcon _tray;
    ToolStripMenuItem _enabledItem = null!; // set by BuildMenu
    MainForm? _form;
    bool _trayHintShown;
    bool _dark;

    public TrayApp(bool startMinimized)
    {
        _settings = new SettingsController(AppSettings.Load(), SaveSettings);
        Theming.Apply(_settings.Theme);
        _dark = Theming.IsDark(_settings.Theme);

        _tray = new NotifyIcon { ContextMenuStrip = BuildMenu(), Visible = true };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowSettings();
        };
        UpdateTrayState();

        _remapper.Start();
        _remapper.Update(_settings.ToConfig());
        _settings.Changed += OnSettingsChanged;

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        if (!startMinimized) ShowSettings();
    }

    public void ShowSettings()
    {
        if (_form == null || _form.IsDisposed) _form = CreateForm();
        _form.Show();
        if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
        _form.Activate();
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
        _enabledItem.Checked = enabled;
        _tray.Icon = enabled ? AppIcons.Enabled : AppIcons.Disabled;
        _tray.Text = enabled ? "Mac Shortcuts (on)" : "Mac Shortcuts (off)";
    }

    /// <summary>Theme changes only affect new windows, so rebuild the menu and settings window.</summary>
    void ApplyTheme()
    {
        _dark = Theming.IsDark(_settings.Theme);
        Theming.Apply(_settings.Theme);

        var oldMenu = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = BuildMenu();
        oldMenu?.Dispose();

        if (_form == null || _form.IsDisposed) return;
        bool visible = _form.Visible;
        var bounds = _form.WindowState == FormWindowState.Normal ? _form.Bounds : _form.RestoreBounds;
        var state = _form.WindowState;
        _form.Dispose();
        _form = null;
        if (!visible) return;

        _form = CreateForm();
        _form.StartPosition = FormStartPosition.Manual;
        _form.Bounds = bounds;
        _form.Show();
        if (state == FormWindowState.Maximized) _form.WindowState = state;
        _form.Activate();
    }

    ContextMenuStrip BuildMenu()
    {
        _enabledItem = new ToolStripMenuItem("Enabled", null, (_, _) => _settings.SetEnabled(!_settings.Enabled))
        {
            Checked = _settings.Enabled,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add(_enabledItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    MainForm CreateForm()
    {
        var form = new MainForm(_settings);
        form.HiddenToTray += (_, _) => ShowTrayHint();
        form.RestartAsAdminRequested += (_, _) =>
        {
            if (Elevation.TryRestartElevated()) ExitThread();
        };
        return form;
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
            _tray.ShowBalloonTip(3000, "Couldn't save settings", ex.Message, ToolTipIcon.Warning);
        }
    }

    void ShowTrayHint()
    {
        if (_trayHintShown) return;
        _trayHintShown = true;
        _tray.ShowBalloonTip(3000, "Mac Shortcuts is still running",
            "It keeps working from the system tray. Right-click the tray icon to exit.", ToolTipIcon.Info);
    }

    void OnSessionSwitch(object? sender, SessionSwitchEventArgs e) => _remapper.ResetKeyState();

    void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // Follow Windows switching between light and dark app mode.
        if (e.Category == UserPreferenceCategory.General && _settings.Theme == AppTheme.System
            && Theming.SystemIsDark != _dark)
            ApplyTheme();
    }

    protected override void ExitThreadCore()
    {
        _settings.Changed -= OnSettingsChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        // Disposing the form saves any pending excluded-apps edit.
        _form?.Dispose();
        _remapper.Dispose();
        _tray.Visible = false;
        _tray.ContextMenuStrip?.Dispose();
        _tray.Dispose();
        base.ExitThreadCore();
    }
}
