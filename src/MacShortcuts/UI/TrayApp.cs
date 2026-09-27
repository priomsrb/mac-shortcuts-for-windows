using MacShortcuts.Platform;
using MacShortcuts.Remapping;
using MacShortcuts.Settings;
using Microsoft.Win32;

namespace MacShortcuts.UI;

/// <summary>Owns the tray icon, the settings window and the remapper for the app's lifetime.</summary>
internal sealed class TrayApp : ApplicationContext
{
    readonly KeyRemapper _remapper = new();
    readonly NotifyIcon _tray;
    ToolStripMenuItem _enabledItem = null!; // set by BuildMenu
    MainForm? _form;
    bool _trayHintShown;
    bool _dark;

    public AppSettings Settings { get; } = AppSettings.Load();

    public TrayApp(bool startMinimized)
    {
        Theming.Apply(Settings.Theme);
        _dark = Theming.IsDark(Settings.Theme);

        _tray = new NotifyIcon { ContextMenuStrip = BuildMenu(), Visible = true };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowSettings();
        };

        _remapper.Start();
        ApplySettings(save: false);

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        if (!startMinimized) ShowSettings();
    }

    public void SetEnabled(bool enabled)
    {
        Settings.Enabled = enabled;
        ApplySettings();
    }

    /// <summary>Persist settings and push them to the running hook.</summary>
    public void ApplySettings(bool save = true)
    {
        if (save) Settings.Save();
        _remapper.Update(Settings.ToConfig());

        _enabledItem.Checked = Settings.Enabled;
        _tray.Icon = Settings.Enabled ? AppIcons.Enabled : AppIcons.Disabled;
        _tray.Text = Settings.Enabled ? "Mac Shortcuts (on)" : "Mac Shortcuts (off)";
        _form?.SyncEnabled();
    }

    public void SetTheme(AppTheme theme)
    {
        Settings.Theme = theme;
        Settings.Save();
        ApplyTheme();
    }

    /// <summary>Theme changes only affect new windows, so rebuild the menu and settings window.</summary>
    void ApplyTheme()
    {
        _dark = Theming.IsDark(Settings.Theme);
        Theming.Apply(Settings.Theme);

        var oldMenu = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = BuildMenu();
        oldMenu?.Dispose();
        _enabledItem.Checked = Settings.Enabled;

        if (_form == null || _form.IsDisposed) return;
        bool visible = _form.Visible;
        var bounds = _form.WindowState == FormWindowState.Normal ? _form.Bounds : _form.RestoreBounds;
        var state = _form.WindowState;
        _form.Dispose();
        _form = null;
        if (!visible) return;

        _form = new MainForm(this) { StartPosition = FormStartPosition.Manual, Bounds = bounds };
        _form.Show();
        if (state == FormWindowState.Maximized) _form.WindowState = state;
        _form.Activate();
    }

    ContextMenuStrip BuildMenu()
    {
        _enabledItem = new ToolStripMenuItem("Enabled", null, (_, _) => SetEnabled(!Settings.Enabled));

        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add(_enabledItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    public void ShowSettings()
    {
        if (_form == null || _form.IsDisposed) _form = new MainForm(this);
        _form.Show();
        if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    public void OnSettingsHidden()
    {
        if (_trayHintShown) return;
        _trayHintShown = true;
        _tray.ShowBalloonTip(3000, "Mac Shortcuts is still running",
            "It keeps working from the system tray. Right-click the tray icon to exit.", ToolTipIcon.Info);
    }

    public void RestartAsAdmin()
    {
        if (Elevation.TryRestartElevated()) ExitThread();
    }

    void OnSessionSwitch(object? sender, SessionSwitchEventArgs e) => _remapper.ResetKeyState();

    void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // Follow Windows switching between light and dark app mode.
        if (e.Category == UserPreferenceCategory.General && Settings.Theme == AppTheme.System
            && Theming.SystemIsDark != _dark)
            ApplyTheme();
    }

    protected override void ExitThreadCore()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _remapper.Dispose();
        _tray.Visible = false;
        _tray.ContextMenuStrip?.Dispose();
        _tray.Dispose();
        _form?.Dispose();
        base.ExitThreadCore();
    }
}
