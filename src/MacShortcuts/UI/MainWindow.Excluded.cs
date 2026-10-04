using MacShortcuts.Interop;
using MacShortcuts.Platform;
using MacShortcuts.UI.Controls;
using MacShortcuts.UI.Drawing;
using static MacShortcuts.Interop.ComCtl32;
using static MacShortcuts.Interop.ComDlg32;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI;

// The Excluded apps page: apps where nothing is remapped, and ways to add them.
internal sealed unsafe partial class MainWindow
{
    const string TipText = "Exclude remote desktop and virtual machine windows when the other computer has its own "
        + "shortcuts set up, and games that use Alt as a key.";

    IntPtr _addEdit;
    IntPtr _addButton;
    IntPtr _pickButton;
    IntPtr _browseButton;
    ListView _excludedList = null!;

    /// <summary>What the list shows; the settings' excluded apps.</summary>
    List<string> _excludedApps = [];

    /// <summary>Descriptions of executables seen running or picked, e.g. "mstsc.exe" → "Remote Desktop Connection".</summary>
    readonly Dictionary<string, string> _descriptions = new(StringComparer.OrdinalIgnoreCase);

    void CreateExcludedPage()
    {
        _addEdit = Edit(Page.Excluded, "Process name, e.g. mstsc.exe");
        _addButton = Button("Add", ButtonKind.Primary, Backdrop.SurfaceAlt, Page.Excluded);
        _pickButton = Button("Pick running app…", ButtonKind.Normal, Backdrop.SurfaceAlt, Page.Excluded);
        _browseButton = Button("Browse…", ButtonKind.Normal, Backdrop.SurfaceAlt, Page.Excluded);

        _excludedList = new ListView(Handle, checkBoxes: false, header: false);
        _pageControls[(int)Page.Excluded].Add(_excludedList.Handle);
        _excludedList.AddColumn("App", Scale(400));
    }

    bool OnExcludedCommand(IntPtr control, int code)
    {
        if (control == _addEdit) return true;
        if (code != BN_CLICKED) return false;
        if (control == _addButton) AddTypedApp();
        else if (control == _pickButton) PickRunningApp();
        else if (control == _browseButton) BrowseForApp();
        else return false;
        return true;
    }

    void OnExcludedPageShown()
    {
        foreach (var app in RunningApps.Find()) _descriptions[app.ProcessName] = app.Description;
    }

    void OnExcludedDpiChanged() => SyncExcluded(force: true);

    /// <summary>Refills the list if the excluded apps changed.</summary>
    void SyncExcluded(bool force = false)
    {
        var apps = _settings.ExcludedApps;
        if (!force && apps.SequenceEqual(_excludedApps)) return;
        _excludedApps = [.. apps];
        _excludedList.Clear();
        foreach (string app in _excludedApps)
        {
            _excludedList.AddItem(0, app);
            if (!_descriptions.ContainsKey(app) && RunningApps.DescribeWindowsApp(app) is { } description) _descriptions[app] = description;
        }
        if (_created) Layout();
    }

    void AddTypedApp()
    {
        string text = GetText(_addEdit).Trim();
        if (text.Length > 0)
        {
            AddApp(text, null);
            SetWindowText(_addEdit, "");
        }
        SetFocus(_addEdit);
    }

    /// <param name="nameOrPath">A process name, with or without ".exe", or the executable's path.</param>
    void AddApp(string nameOrPath, string? description)
    {
        string name = Path.GetFileName(nameOrPath.Trim().Trim('"'));
        if (name.Length == 0) return;
        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name += ".exe";
        if (description != null) _descriptions[name] = description;

        if (!_excludedApps.Contains(name, StringComparer.OrdinalIgnoreCase))
            _settings.SetExcludedApps([.. _settings.ExcludedApps, name]);
        int row = _excludedApps.FindIndex(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (row >= 0) _excludedList.Select(row);
    }

    void RemoveApp(int row)
    {
        if (row < 0 || row >= _excludedApps.Count) return;
        _settings.SetExcludedApps([.. _excludedApps.Where((_, i) => i != row)]);
        if (_excludedApps.Count > 0)
        {
            _excludedList.Select(Math.Min(row, _excludedApps.Count - 1));
            SetFocus(_excludedList.Handle);
        }
    }

    void PickRunningApp()
    {
        var apps = RunningApps.Find().Where(a => !_excludedApps.Contains(a.ProcessName, StringComparer.OrdinalIgnoreCase)).ToList();
        IntPtr menu = CreatePopupMenu();
        if (apps.Count == 0) AppendMenu(menu, MF_STRING | MF_GRAYED, 0, "No other apps are running");
        for (int i = 0; i < apps.Count; i++)
        {
            // The part after the tab lines up in a second column, like a menu's shortcut keys.
            AppendMenu(menu, MF_STRING, (nuint)(i + 1), $"{apps[i].Description.Replace("&", "&&", StringComparison.Ordinal)}\t{apps[i].ProcessName}");
        }
        GetWindowRect(_pickButton, out var button);
        int command = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_NONOTIFY, button.left, button.bottom + Scale(2), Handle, IntPtr.Zero);
        DestroyMenu(menu);
        if (command > 0) AddApp(apps[command - 1].ProcessName, apps[command - 1].Description);
    }

    void BrowseForApp()
    {
        const string filter = "Programs (*.exe)\0*.exe\0\0";
        const int capacity = 1024;
        char* file = stackalloc char[capacity];
        file[0] = '\0';
        fixed (char* filterPtr = filter)
        fixed (char* title = "Choose an app to exclude")
        fixed (char* initialDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))
        {
            var ofn = new OPENFILENAMEW
            {
                lStructSize = (uint)sizeof(OPENFILENAMEW),
                hwndOwner = Handle,
                lpstrFilter = filterPtr,
                nFilterIndex = 1,
                lpstrFile = file,
                nMaxFile = capacity,
                lpstrInitialDir = initialDir,
                lpstrTitle = title,
                Flags = OFN_NOCHANGEDIR | OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST | OFN_DONTADDTORECENT,
            };
            if (!GetOpenFileName(ref ofn)) return;
        }
        string path = new(file);
        AddApp(path, RunningApps.Describe(path));
    }

    IntPtr OnExcludedListNotify(NMHDR* header)
    {
        switch (header->code)
        {
            case NM_CUSTOMDRAW:
                return DrawExcludedRow((NMLVCUSTOMDRAW*)header);

            case NM_CLICK:
                var click = (NMITEMACTIVATE*)header;
                if (click->iItem >= 0 && Paint.Contains(RemoveButtonRect(click->iItem), click->ptAction.X, click->ptAction.Y))
                    RemoveApp(click->iItem);
                return 0;

            case LVN_KEYDOWN when ((NMLVKEYDOWN*)header)->wVKey == VK_DELETE:
                RemoveApp(_excludedList.SelectedIndex);
                return 0;
        }
        return 0;
    }

    RECT RemoveButtonRect(int row)
    {
        var bounds = _excludedList.GetItemRect(row);
        int width = Paint.Measure("Remove", _fonts.Body).cx + Scale(28), height = Scale(30);
        return Paint.Rect(bounds.right - Scale(16) - width, bounds.top + (bounds.bottom - bounds.top - height) / 2, width, height);
    }

    IntPtr DrawExcludedRow(NMLVCUSTOMDRAW* cd)
    {
        if (cd->nmcd.dwDrawStage == CDDS_PREPAINT) return CDRF_NOTIFYITEMDRAW;
        int row = (int)cd->nmcd.dwItemSpec;
        if (cd->nmcd.dwDrawStage != CDDS_ITEMPREPAINT || row >= _excludedApps.Count) return CDRF_DODEFAULT;

        IntPtr hdc = cd->nmcd.hdc;
        var bounds = _excludedList.GetItemRect(row);
        uint back = _excludedList.IsSelected(row) ? _palette.Selection : _palette.Surface;
        Paint.Fill(hdc, bounds, back);

        string name = _excludedApps[row];
        int tileSize = Scale(30), height = bounds.bottom - bounds.top;
        var tile = Paint.Rect(bounds.left + Scale(16), bounds.top + (height - tileSize) / 2, tileSize, tileSize);
        Canvas.Render(hdc, tile, back, c => c.FillRoundRect(0, 0, tileSize, tileSize, Scale(6), _palette.Tile));
        Paint.Text(hdc, name[..1].ToUpperInvariant(), _fonts.SmallStrong, _palette.TileText, tile, DT_SINGLELINE | DT_VCENTER | DT_CENTER);

        var remove = RemoveButtonRect(row);
        int buttonWidth = remove.right - remove.left, buttonHeight = remove.bottom - remove.top;
        Canvas.Render(hdc, remove, back, c => c.RoundRect(0, 0, buttonWidth, buttonHeight, Scale(6), _palette.Surface, _palette.ControlBorder));
        Paint.Text(hdc, "Remove", _fonts.Body, _palette.Text, remove, DT_SINGLELINE | DT_VCENTER | DT_CENTER);

        var text = bounds with { left = tile.right + Scale(12), right = remove.left - Scale(12) };
        if (_descriptions.TryGetValue(name, out string? description))
        {
            int middle = bounds.top + height / 2;
            Paint.Text(hdc, name, _fonts.BodyStrong, _palette.Text, text with { bottom = middle + Scale(1) },
                DT_SINGLELINE | DT_BOTTOM | DT_END_ELLIPSIS);
            Paint.Text(hdc, description, _fonts.Small, _palette.MutedText, text with { top = middle + Scale(2) },
                DT_SINGLELINE | DT_END_ELLIPSIS);
        }
        else
        {
            Paint.Text(hdc, name, _fonts.BodyStrong, _palette.Text, text, DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
        }

        if (row < _excludedApps.Count - 1) Paint.HorizontalLine(hdc, bounds.left, bounds.right, bounds.bottom - 1, _palette.Divider);
        return CDRF_SKIPDEFAULT;
    }

    void LayoutExcludedPage(RECT client)
    {
        int left = RailWidth + Scale(28), right = client.right - Scale(28);
        var title = Paint.Rect(left, Scale(24), right - left, Scale(32));
        const string description = "Nothing is remapped while one of these apps is focused.";
        var descriptionRect = Paint.Rect(left, title.bottom + Scale(2), right - left, Paint.Measure(description, _fonts.Body).cy);
        _art.Add(hdc =>
        {
            Paint.Text(hdc, "Excluded apps", _fonts.Title, _palette.Text, title);
            Paint.Text(hdc, description, _fonts.Body, _palette.MutedText, descriptionRect);
        });

        // The tip, under the panel.
        int tipWidth = Math.Min(Scale(640), right - left);
        int tipLabelWidth = Paint.Measure("Tip", _fonts.BodyStrong).cx + Scale(10);
        int tipTextWidth = tipWidth - 2 * Scale(14) - tipLabelWidth;
        int tipHeight = Paint.MeasureWrapped(TipText, _fonts.Body, tipTextWidth) + 2 * Scale(12);

        // The panel: a strip for adding apps, then one row per app.
        // Rows are a little taller than their image, by the list's own padding.
        int stripHeight = Scale(56);
        int rowHeight = _excludedApps.Count > 0 && _excludedList.GetItemRect(0) is var first && first.bottom > first.top
            ? first.bottom - first.top : Scale(56);
        int panelTop = descriptionRect.bottom + Scale(18);
        int available = client.bottom - Scale(24) - tipHeight - Scale(16) - panelTop;
        int panelHeight = Math.Max(stripHeight + rowHeight + 2, Math.Min(stripHeight + Math.Max(1, _excludedApps.Count) * rowHeight + 2, available));
        var panel = Paint.Rect(left, panelTop, right - left, panelHeight);
        _art.Add(hdc =>
        {
            Paint.Panel(hdc, panel, Scale(8), _palette.Surface, _palette.Border, _palette.Window, _palette.SurfaceAlt, stripHeight);
            Paint.HorizontalLine(hdc, panel.left + 1, panel.right - 1, panel.top + stripHeight, _palette.Border);
        });

        int buttonHeight = Scale(32), buttonTop = panel.top + (stripHeight - buttonHeight) / 2;
        int x = panel.right - Scale(12);
        foreach (var button in (ReadOnlySpan<IntPtr>)[_browseButton, _pickButton, _addButton])
        {
            int width = ButtonWidth(button);
            x -= width;
            Place(button, Paint.Rect(x, buttonTop, width, buttonHeight));
            x -= Scale(8);
        }

        var rows = new RECT { left = panel.left + 1, top = panel.top + stripHeight + 1, right = panel.right - 1, bottom = panel.bottom - 1 };
        if (_excludedApps.Count == 0)
        {
            ShowWindow(_excludedList.Handle, SW_HIDE);
            _art.Add(hdc => Paint.Text(hdc, "No apps are excluded yet.", _fonts.Body, _palette.MutedText, rows,
                DT_SINGLELINE | DT_VCENTER | DT_CENTER));
        }
        else
        {
            ShowWindow(_excludedList.Handle, SW_SHOW);
            Place(_excludedList.Handle, rows);
            // Clipped to the panel's rounded bottom corners; the top corners fall behind the strip's line.
            int width = rows.right - rows.left, height = rows.bottom - rows.top, diameter = 2 * (Scale(8) - 1);
            SetWindowRgn(_excludedList.Handle, CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter), true);
            GetClientRect(_excludedList.Handle, out var listClient);
            _excludedList.SetColumnWidth(0, listClient.right);
        }
        PlaceTextBox(_addEdit, Paint.Rect(panel.left + Scale(12), buttonTop, x - panel.left - Scale(12), buttonHeight), _palette.SurfaceAlt);

        var tip = Paint.Rect(left, panel.bottom + Scale(16), tipWidth, tipHeight);
        _art.Add(hdc =>
        {
            Paint.Panel(hdc, tip, Scale(8), _palette.Tile, _palette.Tile, _palette.Window);
            var label = Paint.Rect(tip.left + Scale(14), tip.top + Scale(12), tipLabelWidth, tipHeight - 2 * Scale(12));
            Paint.Text(hdc, "Tip", _fonts.BodyStrong, _palette.TileText, label, DT_WORDBREAK);
            Paint.Text(hdc, TipText, _fonts.Body, _palette.TileText, label with { left = label.right, right = label.right + tipTextWidth }, DT_WORDBREAK);
        });
    }
}
