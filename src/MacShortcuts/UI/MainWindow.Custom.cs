using MacShortcuts.Interop;
using MacShortcuts.Shortcuts;
using MacShortcuts.UI.Controls;
using MacShortcuts.UI.Drawing;
using static MacShortcuts.Interop.ComCtl32;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI;

// The Custom page: shortcuts the user recorded, and a strip to record another.
internal sealed unsafe partial class MainWindow
{
    const string CustomTipText = "Hold Alt (⌘ Cmd), Ctrl or Win, in any combination and optionally with Shift, and press a key to record the shortcut. "
        + "Custom shortcuts win over built-in ones that use the same keys.";
    const string DefaultRecordHint = "Press the keys… (Esc to cancel)";
    const int VK_ESCAPE = 0x1B;

    IntPtr _triggerBox;
    IntPtr _sendsBox;
    IntPtr _addCustomButton;
    ListView _customList = null!;

    /// <summary>What the list shows: each custom shortcut's keys.</summary>
    List<(string Trigger, string Sends)> _customRows = [];

    /// <summary>The box that is waiting for a key combination, or zero.</summary>
    IntPtr _recording;
    string _recordHint = DefaultRecordHint;
    Chord? _newTrigger;
    Chord? _newSends;

    void CreateCustomPage()
    {
        _triggerBox = Button("Record shortcut", ButtonKind.Capture, Backdrop.SurfaceAlt, Page.Custom);
        _sendsBox = Button("Record keys to send", ButtonKind.Capture, Backdrop.SurfaceAlt, Page.Custom);
        _addCustomButton = Button("Add", ButtonKind.Primary, Backdrop.SurfaceAlt, Page.Custom);

        _customList = new ListView(Handle, checkBoxes: true, header: false);
        _pageControls[(int)Page.Custom].Add(_customList.Handle);
        _customList.AddColumn("Shortcut", Scale(400));
    }

    bool OnCustomCommand(IntPtr control, int code)
    {
        if (control != _triggerBox && control != _sendsBox && control != _addCustomButton) return false;

        if (code == BN_KILLFOCUS)
        {
            if (control == _recording) StopRecording();
        }
        else if (code == BN_CLICKED)
        {
            if (control == _addCustomButton) AddRecordedShortcut();
            else StartRecording(control);
        }
        return true;
    }

    void StartRecording(IntPtr box)
    {
        _recording = box;
        _recordHint = DefaultRecordHint;
        // The hook thread reports each key; the window handles it on the UI thread.
        _setCapture(chord => PostMessage(Handle, WM_CUSTOM_KEY, (IntPtr)(int)chord.Key, (IntPtr)(int)chord.Mods));
        InvalidateRect(box, IntPtr.Zero, false);
    }

    void StopRecording()
    {
        if (_recording == IntPtr.Zero) return;
        _setCapture(null);
        IntPtr box = _recording;
        _recording = IntPtr.Zero;
        InvalidateRect(box, IntPtr.Zero, false);
    }

    void OnCustomKey(Keys key, Mods mods)
    {
        if (_recording == IntPtr.Zero) return;
        if (key == (Keys)VK_ESCAPE && mods == Mods.None)
        {
            StopRecording();
            return;
        }

        var chord = new Chord(key, mods);
        bool isTrigger = _recording == _triggerBox;
        if (isTrigger && !CustomShortcut.IsValidTrigger(chord))
        {
            _recordHint = "Hold Alt, Ctrl and/or Win with the key";
            InvalidateRect(_recording, IntPtr.Zero, false);
            return;
        }

        if (isTrigger) _newTrigger = chord;
        else _newSends = chord;
        StopRecording();
        SetWindowText(_triggerBox, _newTrigger is { } t ? KeyNames.Format(t.Mods, (int)t.Key) : "Record shortcut");
        SetWindowText(_sendsBox, _newSends is { } s ? KeyNames.Format(s.Mods, (int)s.Key) : "Record keys to send");
        InvalidateRect(_triggerBox, IntPtr.Zero, false);
        InvalidateRect(_sendsBox, IntPtr.Zero, false);

        // Carry on to the other box, so a shortcut is recorded with two key presses.
        if (isTrigger && _newSends == null)
        {
            SetFocus(_sendsBox);
            StartRecording(_sendsBox);
        }
    }

    void AddRecordedShortcut()
    {
        if (_newTrigger is not { } trigger)
        {
            SetFocus(_triggerBox);
            StartRecording(_triggerBox);
            return;
        }
        if (_newSends is not { } sends)
        {
            SetFocus(_sendsBox);
            StartRecording(_sendsBox);
            return;
        }

        var shortcut = CustomShortcut.From(trigger, sends);
        if (!_settings.AddCustomShortcut(shortcut))
        {
            MessageBox(Handle, $"There is already a custom shortcut for {shortcut.TriggerText}. Remove it first.", Title, MB_OK | MB_ICONWARNING);
            return;
        }

        _newTrigger = _newSends = null;
        SetWindowText(_triggerBox, "Record shortcut");
        SetWindowText(_sendsBox, "Record keys to send");
        InvalidateRect(_triggerBox, IntPtr.Zero, false);
        InvalidateRect(_sendsBox, IntPtr.Zero, false);
        _customList.Select(_customRows.Count - 1);
        SetFocus(_triggerBox);
    }

    /// <summary>Refills the list if the custom shortcuts changed, or just updates the checkboxes if only they did.</summary>
    void SyncCustom(bool force = false)
    {
        var shortcuts = _settings.CustomShortcuts;
        var rows = shortcuts.Select(c => (c.TriggerText, c.SendsText)).ToList();
        _loading = true;
        if (force || !rows.SequenceEqual(_customRows))
        {
            _customRows = rows;
            _customList.Clear();
            foreach (var _ in rows) _customList.AddItem(0, "");
            if (_created) Layout();
        }
        for (int i = 0; i < shortcuts.Count; i++) _customList.SetChecked(i, shortcuts[i].Enabled);
        _loading = false;
    }

    void OnCustomDpiChanged()
    {
        _loading = true;
        _customList.RecreateCheckBoxes();
        _loading = false;
        SyncCustom(force: true);
    }

    void RemoveCustom(int row)
    {
        if (row < 0 || row >= _customRows.Count) return;
        _settings.RemoveCustomShortcut(row);
        if (_customRows.Count > 0)
        {
            _customList.Select(Math.Min(row, _customRows.Count - 1));
            SetFocus(_customList.Handle);
        }
    }

    IntPtr OnCustomListNotify(NMHDR* header)
    {
        switch (header->code)
        {
            case LVN_ITEMCHANGED when !_loading && ListView.IsCheckToggle((NMLISTVIEW*)header):
                int row = ((NMLISTVIEW*)header)->iItem;
                if (row >= 0 && row < _customRows.Count) _settings.SetCustomShortcutEnabled(row, _customList.IsChecked(row));
                return 0;

            case NM_CUSTOMDRAW:
                return DrawCustomRow((NMLVCUSTOMDRAW*)header);

            case NM_CLICK:
                var click = (NMITEMACTIVATE*)header;
                if (click->iItem >= 0 && Paint.Contains(CustomRemoveRect(click->iItem), click->ptAction.X, click->ptAction.Y))
                    RemoveCustom(click->iItem);
                return 0;

            case LVN_KEYDOWN when ((NMLVKEYDOWN*)header)->wVKey == VK_DELETE:
                RemoveCustom(_customList.SelectedIndex);
                return 0;
        }
        return 0;
    }

    RECT CustomRemoveRect(int row)
    {
        var bounds = _customList.GetItemRect(row);
        int width = Paint.Measure("Remove", _fonts.Body).cx + Scale(28), height = Scale(30);
        return Paint.Rect(bounds.right - Scale(16) - width, bounds.top + (bounds.bottom - bounds.top - height) / 2, width, height);
    }

    IntPtr DrawCustomRow(NMLVCUSTOMDRAW* cd)
    {
        int row = (int)cd->nmcd.dwItemSpec;
        switch (cd->nmcd.dwDrawStage)
        {
            case CDDS_PREPAINT:
                return CDRF_NOTIFYITEMDRAW;

            case CDDS_ITEMPREPAINT:
                cd->nmcd.uItemState &= ~(CDIS_SELECTED | CDIS_FOCUS | CDIS_HOT);
                cd->clrText = (int)_palette.Text;
                cd->clrTextBk = (int)(_customList.IsSelected(row) ? _palette.Selection : _palette.Surface);
                return CDRF_NOTIFYPOSTPAINT;

            case CDDS_ITEMPOSTPAINT when row < _customRows.Count:
                IntPtr hdc = cd->nmcd.hdc;
                var bounds = _customList.GetItemRect(row);
                int left = _customList.GetItemRect(row, LVIR_LABEL).left;
                int arrowWidth = Scale(36), keysWidth = Scale(250);

                var remove = CustomRemoveRect(row);
                int buttonWidth = remove.right - remove.left, buttonHeight = remove.bottom - remove.top;
                uint back = _customList.IsSelected(row) ? _palette.Selection : _palette.Surface;
                Canvas.Render(hdc, remove, back, c => c.RoundRect(0, 0, buttonWidth, buttonHeight, Scale(6), _palette.Surface, _palette.ControlBorder));
                Paint.Text(hdc, "Remove", _fonts.Body, _palette.Text, remove, DT_SINGLELINE | DT_VCENTER | DT_CENTER);

                var (trigger, sends) = _customRows[row];
                var triggerCell = new RECT { left = left, top = bounds.top + 1, right = left + keysWidth, bottom = bounds.bottom - 1 };
                var arrow = triggerCell with { left = triggerCell.right, right = triggerCell.right + arrowWidth };
                var sendsCell = arrow with { left = arrow.right, right = remove.left - Scale(12) };
                KeyCaps.Draw(hdc, triggerCell, trigger, true, _palette, _fonts.Key, _fonts.KeySymbol, _dpi);
                Paint.Text(hdc, "→", _fonts.Body, _palette.MutedText, arrow, DT_SINGLELINE | DT_VCENTER | DT_CENTER);
                KeyCaps.Draw(hdc, sendsCell, sends, false, _palette, _fonts.Key, _fonts.KeySymbol, _dpi);

                Paint.HorizontalLine(hdc, bounds.left, bounds.right, bounds.bottom - 1, _palette.Divider);
                return CDRF_DODEFAULT;
        }
        return CDRF_DODEFAULT;
    }

    void LayoutCustomPage(RECT client)
    {
        int left = RailWidth + Scale(28), right = client.right - Scale(28);
        var title = Paint.Rect(left, Scale(24), right - left, Scale(32));
        const string description = "Your own shortcuts: press one combination, and another is sent instead.";
        var descriptionRect = Paint.Rect(left, title.bottom + Scale(2), right - left, Paint.Measure(description, _fonts.Body).cy);
        _art.Add(hdc =>
        {
            Paint.Text(hdc, "Custom shortcuts", _fonts.Title, _palette.Text, title);
            Paint.Text(hdc, description, _fonts.Body, _palette.MutedText, descriptionRect);
        });

        // The tip, under the panel.
        int tipWidth = Math.Min(Scale(640), right - left);
        int tipLabelWidth = Paint.Measure("Tip", _fonts.BodyStrong).cx + Scale(10);
        int tipTextWidth = tipWidth - 2 * Scale(14) - tipLabelWidth;
        int tipHeight = Paint.MeasureWrapped(CustomTipText, _fonts.Body, tipTextWidth) + 2 * Scale(12);

        // The panel: a strip for recording a shortcut, then one row per custom shortcut.
        int stripHeight = Scale(56);
        int rowHeight = _customRows.Count > 0 && _customList.GetItemRect(0) is var first && first.bottom > first.top
            ? first.bottom - first.top : Scale(56);
        int panelTop = descriptionRect.bottom + Scale(18);
        int available = client.bottom - Scale(24) - tipHeight - Scale(16) - panelTop;
        int panelHeight = Math.Max(stripHeight + rowHeight + 2, Math.Min(stripHeight + Math.Max(1, _customRows.Count) * rowHeight + 2, available));
        var panel = Paint.Rect(left, panelTop, right - left, panelHeight);
        _art.Add(hdc =>
        {
            Paint.Panel(hdc, panel, Scale(8), _palette.Surface, _palette.Border, _palette.Window, _palette.SurfaceAlt, stripHeight);
            Paint.HorizontalLine(hdc, panel.left + 1, panel.right - 1, panel.top + stripHeight, _palette.Border);
        });

        int buttonHeight = Scale(32), buttonTop = panel.top + (stripHeight - buttonHeight) / 2;
        int addWidth = ButtonWidth(_addCustomButton);
        Place(_addCustomButton, Paint.Rect(panel.right - Scale(12) - addWidth, buttonTop, addWidth, buttonHeight));

        int arrowWidth = Scale(36);
        int boxWidth = Math.Min(Scale(260), (panel.right - Scale(12) - addWidth - Scale(8) - arrowWidth - (panel.left + Scale(12))) / 2);
        var triggerBox = Paint.Rect(panel.left + Scale(12), buttonTop, boxWidth, buttonHeight);
        var arrow = Paint.Rect(triggerBox.right, buttonTop, arrowWidth, buttonHeight);
        Place(_triggerBox, triggerBox);
        Place(_sendsBox, Paint.Rect(arrow.right, buttonTop, boxWidth, buttonHeight));
        _art.Add(hdc => Paint.Text(hdc, "→", _fonts.Body, _palette.MutedText, arrow, DT_SINGLELINE | DT_VCENTER | DT_CENTER));

        var rows = new RECT { left = panel.left + 1, top = panel.top + stripHeight + 1, right = panel.right - 1, bottom = panel.bottom - 1 };
        if (_customRows.Count == 0)
        {
            ShowWindow(_customList.Handle, SW_HIDE);
            _art.Add(hdc => Paint.Text(hdc, "No custom shortcuts yet.", _fonts.Body, _palette.MutedText, rows,
                DT_SINGLELINE | DT_VCENTER | DT_CENTER));
        }
        else
        {
            ShowWindow(_customList.Handle, SW_SHOW);
            Place(_customList.Handle, rows);
            // Clipped to the panel's rounded bottom corners; the top corners fall behind the strip's line.
            int width = rows.right - rows.left, height = rows.bottom - rows.top, diameter = 2 * (Scale(8) - 1);
            SetWindowRgn(_customList.Handle, CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter), true);
            GetClientRect(_customList.Handle, out var listClient);
            _customList.SetColumnWidth(0, listClient.right);
        }

        var tip = Paint.Rect(left, panel.bottom + Scale(16), tipWidth, tipHeight);
        _art.Add(hdc =>
        {
            Paint.Panel(hdc, tip, Scale(8), _palette.Tile, _palette.Tile, _palette.Window);
            var label = Paint.Rect(tip.left + Scale(14), tip.top + Scale(12), tipLabelWidth, tipHeight - 2 * Scale(12));
            Paint.Text(hdc, "Tip", _fonts.BodyStrong, _palette.TileText, label, DT_WORDBREAK);
            Paint.Text(hdc, CustomTipText, _fonts.Body, _palette.TileText, label with { left = label.right, right = label.right + tipTextWidth }, DT_WORDBREAK);
        });
    }
}
