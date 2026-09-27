namespace MacShortcuts;

internal sealed class MainForm : Form
{
    readonly TrayApp _app;
    readonly CheckBox _enabled;
    readonly CheckBox _leftAlt;
    readonly CheckBox _rightAlt;
    readonly CheckBox _startup;
    readonly ListView _list;
    readonly TextBox _excluded;
    readonly System.Windows.Forms.Timer _excludedSaveTimer = new() { Interval = 600 };
    bool _loading;

    AppSettings Settings => _app.Settings;

    public MainForm(TrayApp app)
    {
        _app = app;

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

        var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
        options.Controls.AddRange([_enabled, _leftAlt, _rightAlt, _startup]);

        if (Elevation.IsAdmin)
        {
            options.Controls.Add(new Label { Text = "Running as administrator", AutoSize = true, Margin = new Padding(0, 6, 0, 0), ForeColor = SystemColors.GrayText });
        }
        else
        {
            var adminLink = new LinkLabel { Text = "Restart as administrator", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            tips.SetToolTip(adminLink, "Needed for shortcuts to work while an admin app (Task Manager, admin terminal…) is focused.");
            adminLink.LinkClicked += (_, _) => _app.RestartAsAdmin();
            options.Controls.Add(adminLink);
        }
        root.Controls.Add(options);

        // Shortcut list
        _list = new ListView
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
            _list.Items.Add(new ListViewItem([shortcut.Trigger, shortcut.Sends, shortcut.Description], group) { Tag = shortcut });
        }
        _list.ItemChecked += OnItemChecked;
        root.Controls.Add(_list);

        // Excluded apps
        var excludedBox = new GroupBox
        {
            Text = "Excluded apps",
            Dock = DockStyle.Fill,
            Height = LogicalToDeviceUnits(130),
            Margin = new Padding(0, 10, 0, 0),
            Padding = new Padding(8),
        };
        _excluded = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
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
        buttons.Controls.Add(MakeButton("Enable all", (_, _) => SetAll(true)));
        buttons.Controls.Add(MakeButton("Disable all", (_, _) => SetAll(false)));
        buttons.Controls.Add(MakeButton("Reset to defaults", (_, _) => ResetDefaults()));
        buttons.Controls.Add(new Label
        {
            Text = "Changes apply immediately. Closing this window keeps the app running in the tray.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(12, 8, 0, 0),
        });
        root.Controls.Add(buttons);

        LoadFromSettings();

        _enabled.CheckedChanged += (_, _) => { if (!_loading) _app.SetEnabled(_enabled.Checked); };
        _leftAlt.CheckedChanged += (_, _) => { if (!_loading) { Settings.UseLeftAlt = _leftAlt.Checked; _app.ApplySettings(); } };
        _rightAlt.CheckedChanged += (_, _) => { if (!_loading) { Settings.UseRightAlt = _rightAlt.Checked; _app.ApplySettings(); } };
        _startup.CheckedChanged += (_, _) => { if (!_loading) SetStartup(_startup.Checked); };
    }

    /// <summary>Called by the tray when "Enabled" is toggled there.</summary>
    public void SyncEnabled()
    {
        _loading = true;
        _enabled.Checked = Settings.Enabled;
        _loading = false;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveExcluded();
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            _app.OnSettingsHidden();
        }
        base.OnFormClosing(e);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitLastColumn();
    }

    void LoadFromSettings()
    {
        _loading = true;
        _enabled.Checked = Settings.Enabled;
        _leftAlt.Checked = Settings.UseLeftAlt;
        _rightAlt.Checked = Settings.UseRightAlt;
        _startup.Checked = StartWithWindows.IsEnabled;
        foreach (ListViewItem item in _list.Items)
            item.Checked = Settings.IsEnabled((ShortcutDef)item.Tag!);
        _excluded.Text = string.Join(Environment.NewLine, Settings.ExcludedApps);
        _loading = false;
    }

    void OnItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        if (_loading) return;
        Settings.Shortcuts[((ShortcutDef)e.Item.Tag!).Id] = e.Item.Checked;
        _app.ApplySettings();
    }

    void SetAll(bool enabled)
    {
        foreach (var shortcut in ShortcutCatalog.All) Settings.Shortcuts[shortcut.Id] = enabled;
        _app.ApplySettings();
        LoadFromSettings();
    }

    void ResetDefaults()
    {
        Settings.Shortcuts.Clear();
        _app.ApplySettings();
        LoadFromSettings();
    }

    void SaveExcluded()
    {
        _excludedSaveTimer.Stop();
        var apps = _excluded.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (apps.SequenceEqual(Settings.ExcludedApps)) return;
        Settings.ExcludedApps = apps;
        _app.ApplySettings();
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
