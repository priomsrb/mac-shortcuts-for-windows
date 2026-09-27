using Microsoft.Win32;

namespace MacShortcuts;

/// <summary>Owns the tray icon, the settings window and the remapper for the app's lifetime.</summary>
internal sealed class TrayApp : ApplicationContext
{
    readonly KeyRemapper _remapper = new();
    readonly NotifyIcon _tray;
    readonly ToolStripMenuItem _enabledItem;
    MainForm? _form;
    bool _trayHintShown;

    public AppSettings Settings { get; } = AppSettings.Load();

    public TrayApp(bool startMinimized)
    {
        _enabledItem = new ToolStripMenuItem("Enabled", null, (_, _) => SetEnabled(!Settings.Enabled));

        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add(_enabledItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowSettings();
        };

        _remapper.Start();
        ApplySettings(save: false);

        SystemEvents.SessionSwitch += OnSessionSwitch;

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

    protected override void ExitThreadCore()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _remapper.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _form?.Dispose();
        base.ExitThreadCore();
    }
}
