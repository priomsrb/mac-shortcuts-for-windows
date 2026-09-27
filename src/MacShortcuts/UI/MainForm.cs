using MacShortcuts.Platform;
using MacShortcuts.Settings;
using MacShortcuts.Shortcuts;
using MacShortcuts.UI.Controls;

namespace MacShortcuts.UI;

internal sealed class MainForm : Form
{
    readonly SettingsController _settings;
    readonly CheckBox _enabled;
    readonly CheckBox _leftAlt;
    readonly CheckBox _rightAlt;
    readonly CheckBox _startup;
    readonly ComboBox _theme;
    readonly ThemedListView _list;
    readonly TextBox _excluded;
    readonly System.Windows.Forms.Timer _excludedSaveTimer = new() { Interval = 600 };
    bool _loading;

    /// <summary>The user closed the window; the app keeps running in the tray.</summary>
    public event EventHandler? HiddenToTray;

    public event EventHandler? RestartAsAdminRequested;

    public MainForm(SettingsController settings)
    {
        _settings = settings;

        Text = "Mac Shortcuts for Windows";
        Icon = AppIcons.Enabled;
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(LogicalToDeviceUnits(900), LogicalToDeviceUnits(760));
        MinimumSize = new Size(LogicalToDeviceUnits(640), LogicalToDeviceUnits(480));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // General options
        var tips = new ToolTip();
        _enabled = MakeCheckBox("Enable remapping");
        _enabled.Font = new Font(Font, FontStyle.Bold);
        _leftAlt = MakeCheckBox("Left Alt acts as ⌘ Cmd");
        _rightAlt = MakeCheckBox("Right Alt acts as ⌘ Cmd");
        tips.SetToolTip(_rightAlt, "On keyboard layouts with AltGr, this replaces AltGr+key characters (e.g. €).");
        _startup = MakeCheckBox("Start with Windows");
        _theme = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = LogicalToDeviceUnits(80), Margin = new Padding(4, 2, 20, 0) };
        _theme.Items.AddRange(Enum.GetNames<AppTheme>());

        var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
        options.Controls.AddRange([_enabled, _leftAlt, _rightAlt, _startup,
            new Label { Text = "Theme:", AutoSize = true, Margin = new Padding(0, 6, 0, 0) }, _theme]);

        if (Elevation.IsAdmin)
        {
            options.Controls.Add(new Label { Text = "Running as administrator", AutoSize = true, Margin = new Padding(0, 6, 0, 0), ForeColor = SystemColors.GrayText });
        }
        else
        {
            var adminLink = new LinkLabel { Text = "Restart as administrator", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            if (Application.IsDarkModeEnabled) adminLink.LinkColor = adminLink.ActiveLinkColor = Theming.DarkAccentText;
            tips.SetToolTip(adminLink, "Needed for shortcuts to work while an admin app (Task Manager, admin terminal…) is focused.");
            adminLink.LinkClicked += (_, _) => RestartAsAdminRequested?.Invoke(this, EventArgs.Empty);
            options.Controls.Add(adminLink);
        }
        root.Controls.Add(options);

        // Shortcut list
        _list = new ThemedListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            CheckBoxes = true,
            FullRowSelect = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _list.Columns.Add("Mac-style shortcut", LogicalToDeviceUnits(210));
        _list.Columns.Add("Sends", LogicalToDeviceUnits(220));
        _list.Columns.Add("Action", LogicalToDeviceUnits(380));
        _list.Resize += (_, _) => FitLastColumn();

        var groups = new Dictionary<string, ListViewGroup>();
        foreach (var shortcut in ShortcutCatalog.All)
        {
            if (!groups.TryGetValue(shortcut.Category, out var group))
            {
                group = new ListViewGroup(shortcut.Category);
                groups[shortcut.Category] = group;
                _list.Groups.Add(group);
            }
            _list.Items.Add(new ListViewItem([shortcut.TriggerText, shortcut.SendsText, shortcut.Description], group) { Tag = shortcut });
        }
        if (Application.IsDarkModeEnabled) _list.UseDarkCheckBoxes();
        _list.ItemChecked += OnItemChecked;
        root.Controls.Add(_list);

        // Excluded apps
        var excludedBox = new ThemedGroupBox
        {
            Text = "Excluded apps",
            Dock = DockStyle.Fill,
            Height = LogicalToDeviceUnits(130),
            Margin = new Padding(0, 10, 0, 0),
            Padding = new Padding(8),
        };
        _excluded = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
        // The default 3D border is near-white in dark mode; the single-line one is a subtle grey.
        if (Application.IsDarkModeEnabled) _excluded.BorderStyle = BorderStyle.FixedSingle;
        excludedBox.Controls.Add(_excluded);
        excludedBox.Controls.Add(new Label
        {
            Text = "Nothing is remapped while these apps are focused. One process name per line, e.g. mstsc.exe",
            Dock = DockStyle.Top,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Padding = new Padding(0, 0, 0, 4),
        });
        _excluded.TextChanged += (_, _) =>
        {
            if (_loading) return;
            _excludedSaveTimer.Stop();
            _excludedSaveTimer.Start();
        };
        _excludedSaveTimer.Tick += (_, _) => SaveExcluded();
        root.Controls.Add(excludedBox);

        // Buttons
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        buttons.Controls.Add(MakeButton("Enable all", (_, _) => _settings.SetAllShortcutsEnabled(true)));
        buttons.Controls.Add(MakeButton("Disable all", (_, _) => _settings.SetAllShortcutsEnabled(false)));
        buttons.Controls.Add(MakeButton("Reset to defaults", (_, _) => _settings.ResetShortcuts()));
        buttons.Controls.Add(new Label
        {
            Text = "Changes apply immediately. Closing this window keeps the app running in the tray.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(12, 8, 0, 0),
        });
        root.Controls.Add(buttons);

        LoadFromSettings();
        _settings.Changed += OnSettingsChanged;

        _enabled.CheckedChanged += (_, _) => { if (!_loading) _settings.SetEnabled(_enabled.Checked); };
        _leftAlt.CheckedChanged += (_, _) => { if (!_loading) _settings.SetUseLeftAlt(_leftAlt.Checked); };
        _rightAlt.CheckedChanged += (_, _) => { if (!_loading) _settings.SetUseRightAlt(_rightAlt.Checked); };
        _startup.CheckedChanged += (_, _) => { if (!_loading) SetStartup(_startup.Checked); };
        // Deferred: applying a theme recreates this window.
        _theme.SelectedIndexChanged += (_, _) => { if (!_loading) BeginInvoke(() => _settings.SetTheme((AppTheme)_theme.SelectedIndex)); };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveExcluded();
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            HiddenToTray?.Invoke(this, EventArgs.Empty);
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        // The window is disposed without closing when the theme changes or the app exits.
        if (disposing)
        {
            SaveExcluded();
            _settings.Changed -= OnSettingsChanged;
            _excludedSaveTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitLastColumn();
    }

    void LoadFromSettings()
    {
        _loading = true;
        _startup.Checked = StartWithWindows.IsEnabled;
        _theme.SelectedIndex = (int)_settings.Theme;
        _excluded.Text = string.Join(Environment.NewLine, _settings.ExcludedApps);
        _loading = false;
        SyncToggles();
    }

    /// <summary>Settings can also change from the tray menu or the buttons below the list.</summary>
    void OnSettingsChanged(object? sender, EventArgs e)
    {
        // A handler that ran before this one may have disposed the window (e.g. to apply a theme).
        if (!IsDisposed) SyncToggles();
    }

    /// <summary>
    /// Updates the checkboxes. The excluded-apps text is left alone so typing in it isn't disturbed,
    /// and the theme can only change here.
    /// </summary>
    void SyncToggles()
    {
        _loading = true;
        _enabled.Checked = _settings.Enabled;
        _leftAlt.Checked = _settings.UseLeftAlt;
        _rightAlt.Checked = _settings.UseRightAlt;
        foreach (ListViewItem item in _list.Items)
            item.Checked = _settings.IsEnabled((ShortcutDef)item.Tag!);
        _loading = false;
    }

    void OnItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        if (_loading) return;
        _settings.SetShortcutEnabled((ShortcutDef)e.Item.Tag!, e.Item.Checked);
    }

    void SaveExcluded()
    {
        _excludedSaveTimer.Stop();
        _settings.SetExcludedApps(_excluded.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList());
    }

    void SetStartup(bool enabled)
    {
        try
        {
            StartWithWindows.Set(enabled);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't update the startup setting:\n{ex.Message}", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _loading = true;
            _startup.Checked = StartWithWindows.IsEnabled;
            _loading = false;
        }
    }

    void FitLastColumn()
    {
        if (_list.Columns.Count < 3) return;
        int used = _list.Columns[0].Width + _list.Columns[1].Width;
        int available = _list.ClientSize.Width - used;
        if (available > 100) _list.Columns[2].Width = available;
    }

    static CheckBox MakeCheckBox(string text) =>
        new() { Text = text, AutoSize = true, Margin = new Padding(0, 4, 20, 4) };

    static Button MakeButton(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, AutoSize = true, Padding = new Padding(6, 2, 6, 2) };
        button.Click += onClick;
        return button;
    }
}
