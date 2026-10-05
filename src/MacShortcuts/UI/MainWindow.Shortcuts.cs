using MacShortcuts.Interop;
using MacShortcuts.Shortcuts;
using MacShortcuts.UI.Controls;
using MacShortcuts.UI.Drawing;
using static MacShortcuts.Interop.ComCtl32;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI;

// The Shortcuts page: categories on the left, a filterable table of shortcuts on the right.
internal sealed unsafe partial class MainWindow
{
    const string AllCategories = "All";

    /// <summary>"All", then the catalog's categories in order.</summary>
    static readonly string[] Categories = [AllCategories, .. ShortcutCatalog.All.Select(s => s.Category).Distinct()];

    IntPtr _categories;
    IntPtr _filter;
    IntPtr _reset;
    ListView _shortcutList = null!;

    /// <summary>The catalog index of each row in the list.</summary>
    readonly List<int> _rows = [];

    int CategoryItemHeight => Scale(30);

    string SelectedCategory
    {
        get
        {
            int index = (int)SendMessage(_categories, LB_GETCURSEL, IntPtr.Zero, IntPtr.Zero);
            return index > 0 && index < Categories.Length ? Categories[index] : AllCategories;
        }
    }

    void CreateShortcutsPage()
    {
        _categories = Child(Page.Shortcuts, "LISTBOX", null,
            LBS_NOTIFY | LBS_OWNERDRAWFIXED | LBS_HASSTRINGS | LBS_NOINTEGRALHEIGHT | WS_TABSTOP);
        foreach (string category in Categories) SendMessage(_categories, LB_ADDSTRING, IntPtr.Zero, category);
        SendMessage(_categories, LB_SETCURSEL, IntPtr.Zero, IntPtr.Zero);

        _filter = Edit(Page.Shortcuts, "Filter shortcuts");
        _reset = Button("Reset to defaults", ButtonKind.Normal, Backdrop.Window, Page.Shortcuts);

        _shortcutList = new ListView(Handle, checkBoxes: true, header: true);
        _pageControls[(int)Page.Shortcuts].Add(_shortcutList.Handle);
        _shortcutList.AddColumn("Mac-style shortcut", Scale(210));
        _shortcutList.AddColumn("Sends", Scale(200));
        _shortcutList.AddColumn("Action", Scale(300));
        for (int i = 1; i < Categories.Length; i++) _shortcutList.AddGroup(i, Categories[i]);
        RebuildShortcutList();
    }

    bool OnShortcutsCommand(IntPtr control, int code)
    {
        if (control == _filter || control == _categories)
        {
            if (code == EN_CHANGE || code == LBN_SELCHANGE)
            {
                RebuildShortcutList();
                Layout();
            }
            return true;
        }
        if (control == _reset && code == BN_CLICKED)
        {
            _settings.ResetShortcuts();
            return true;
        }
        return false;
    }

    /// <summary>Fills the list with the shortcuts in the selected category that match the filter.</summary>
    void RebuildShortcutList()
    {
        string category = SelectedCategory;
        string filter = GetText(_filter).Trim();
        bool all = category == AllCategories;

        _loading = true;
        SendMessage(_shortcutList.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        _shortcutList.Clear();
        _rows.Clear();
        _shortcutList.ItemIndent = Scale(10);
        _shortcutList.EnableGroups(all);
        var catalog = ShortcutCatalog.All;
        for (int i = 0; i < catalog.Count; i++)
        {
            var shortcut = catalog[i];
            if (!all && shortcut.Category != category) continue;
            if (filter.Length > 0 && !Matches(shortcut, filter)) continue;
            int row = _shortcutList.AddItem(Array.IndexOf(Categories, shortcut.Category), shortcut.TriggerText, shortcut.SendsText, shortcut.Description);
            _shortcutList.SetChecked(row, _settings.IsEnabled(shortcut));
            _rows.Add(i);
        }
        SendMessage(_shortcutList.Handle, WM_SETREDRAW, 1, IntPtr.Zero);
        InvalidateRect(_shortcutList.Handle, IntPtr.Zero, true);
        _loading = false;
    }

    static bool Matches(ShortcutDef shortcut, string filter) =>
        shortcut.TriggerText.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || shortcut.SendsText.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || shortcut.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || shortcut.Category.Contains(filter, StringComparison.OrdinalIgnoreCase);

    void SyncShortcutList()
    {
        for (int row = 0; row < _rows.Count; row++)
            _shortcutList.SetChecked(row, _settings.IsEnabled(ShortcutCatalog.All[_rows[row]]));
        InvalidateRect(_categories, IntPtr.Zero, false);
    }

    /// <returns>How many shortcuts in <paramref name="category"/> are on, and how many there are.</returns>
    (int On, int Total) CountEnabled(string category)
    {
        int on = 0, total = 0;
        foreach (var shortcut in ShortcutCatalog.All)
        {
            if (category != AllCategories && shortcut.Category != category) continue;
            total++;
            if (_settings.IsEnabled(shortcut)) on++;
        }
        return (on, total);
    }

    void OnShortcutsDpiChanged()
    {
        for (int i = 0; i < 2; i++) _shortcutList.SetColumnWidth(i, Scale(i == 0 ? 210 : 200));
        _loading = true;
        _shortcutList.RecreateCheckBoxes();
        _loading = false;
        // Re-adds the rows with their checks and an indent at the new DPI.
        RebuildShortcutList();
        InvalidateRect(_categories, IntPtr.Zero, false);
    }

    IntPtr OnShortcutListNotify(NMHDR* header)
    {
        switch (header->code)
        {
            case LVN_ITEMCHANGED when !_loading && ListView.IsCheckToggle((NMLISTVIEW*)header):
                int row = ((NMLISTVIEW*)header)->iItem;
                if (row >= 0 && row < _rows.Count)
                    _settings.SetShortcutEnabled(ShortcutCatalog.All[_rows[row]], _shortcutList.IsChecked(row));
                return 0;

            case LVN_ENDSCROLL:
                // Scrolling can leave the column headers partly painted over; draw them again.
                InvalidateRect(_shortcutList.Header, IntPtr.Zero, true);
                return 0;

            case NM_CUSTOMDRAW:
                return DrawShortcutRow((NMLVCUSTOMDRAW*)header);
        }
        return 0;
    }

    IntPtr DrawShortcutRow(NMLVCUSTOMDRAW* cd)
    {
        switch (cd->nmcd.dwDrawStage)
        {
            // Group headers get a single prepaint notification of their own.
            case CDDS_PREPAINT when cd->dwItemType == LVCDI_GROUP:
                _shortcutList.DrawGroupHeader(cd->nmcd.hdc, (int)cd->nmcd.dwItemSpec, cd->rcText, _palette, _fonts.BodyStrong);
                return CDRF_SKIPDEFAULT;

            case CDDS_PREPAINT:
                return CDRF_NOTIFYITEMDRAW | CDRF_NOTIFYPOSTPAINT;

            case CDDS_POSTPAINT:
                // The Explorer theme also draws column lines below the last row.
                GetClientRect(_shortcutList.Handle, out var client);
                int top = _shortcutList.Count > 0 ? _shortcutList.GetItemRect(_shortcutList.Count - 1).bottom : client.top;
                if (top < client.bottom) Paint.Fill(cd->nmcd.hdc, client with { top = top }, _palette.Surface);
                _shortcutList.FillGroupGaps(cd->nmcd.hdc, _palette.Surface);
                return CDRF_DODEFAULT;

            case CDDS_ITEMPREPAINT:
                ColorRow(cd);
                return CDRF_NOTIFYSUBITEMDRAW | CDRF_NOTIFYPOSTPAINT;

            case CDDS_SUBITEMPREPAINT:
                ColorRow(cd);
                if (cd->iSubItem == 1) cd->clrText = (int)_palette.MutedText;
                SelectObject(cd->nmcd.hdc, cd->iSubItem switch { 0 => _fonts.BodyStrong, _ => _fonts.Body });
                return CDRF_NEWFONT;

            case CDDS_ITEMPOSTPAINT:
                var bounds = _shortcutList.GetItemRect((int)cd->nmcd.dwItemSpec);
                // The Explorer theme draws lines between the columns; the design has none.
                uint back = _shortcutList.IsSelected((int)cd->nmcd.dwItemSpec) ? _palette.Selection : _palette.Surface;
                int x = bounds.left;
                for (int column = 0; column < 2; column++)
                {
                    x += _shortcutList.GetColumnWidth(column);
                    Paint.Fill(cd->nmcd.hdc, Paint.Rect(x - Scale(2), bounds.top, Scale(4), bounds.bottom - bounds.top), back);
                }
                Paint.HorizontalLine(cd->nmcd.hdc, bounds.left, bounds.right, bounds.bottom - 1, _palette.Divider);
                return CDRF_DODEFAULT;
        }
        return CDRF_DODEFAULT;
    }

    /// <summary>Draws the selected row in the palette's colours instead of the system highlight.</summary>
    void ColorRow(NMLVCUSTOMDRAW* cd)
    {
        bool selected = _shortcutList.IsSelected((int)cd->nmcd.dwItemSpec);
        cd->nmcd.uItemState &= ~(CDIS_SELECTED | CDIS_FOCUS | CDIS_HOT);
        cd->clrText = (int)_palette.Text;
        cd->clrTextBk = (int)(selected ? _palette.Selection : _palette.Surface);
    }

    void DrawCategory(DRAWITEMSTRUCT* item)
    {
        IntPtr hdc = item->hDC;
        var rc = item->rcItem;
        Paint.Fill(hdc, rc, _palette.Window);
        if (item->itemID < 0 || item->itemID >= Categories.Length) return;

        string category = Categories[item->itemID];
        bool selected = (item->itemState & ODS_SELECTED) != 0;
        if (selected)
        {
            int width = rc.right - rc.left, height = rc.bottom - rc.top;
            Canvas.Render(hdc, rc, _palette.Window, c => c.FillRoundRect(0, 1, width, height - 2, Scale(6), _palette.Selection));
        }
        var (on, total) = CountEnabled(category);
        var text = rc with { left = rc.left + Scale(10), right = rc.right - Scale(10) };
        Paint.Text(hdc, $"{on}/{total}", _fonts.Small, _palette.MutedText, text, DT_SINGLELINE | DT_VCENTER | DT_RIGHT);
        Paint.Text(hdc, category, selected ? _fonts.BodyStrong : _fonts.Body, selected ? _palette.SelectionText : _palette.Text, text);
        if ((item->itemState & ODS_FOCUS) != 0 && (item->itemState & ODS_NOFOCUSRECT) == 0)
            DrawFocusRing(hdc, rc with { top = rc.top + 1, bottom = rc.bottom - 1 }, Scale(6));
    }

    void LayoutShortcutsPage(RECT client)
    {
        // The categories column.
        int asideLeft = RailWidth, asideRight = asideLeft + Scale(200);
        var asideTitle = Paint.Rect(asideLeft + Scale(22), Scale(18), Scale(160), Scale(24));
        int listTop = asideTitle.bottom + Scale(12);
        int listHeight = Math.Min(Categories.Length * CategoryItemHeight + Scale(2), client.bottom - listTop - Scale(12));
        Place(_categories, Paint.Rect(asideLeft + Scale(12), listTop, asideRight - asideLeft - Scale(24), listHeight));
        _art.Add(hdc =>
        {
            Paint.Text(hdc, "Shortcuts", _fonts.Subheading, _palette.Text, asideTitle);
            Paint.VerticalLine(hdc, asideRight, client.top, client.bottom, _palette.Border);
        });

        // The header row: category, count, filter box and reset button.
        int left = asideRight + 1 + Scale(20), right = client.right - Scale(20);
        var row = Paint.Rect(left, Scale(16), right - left, Scale(32));
        int resetWidth = ButtonWidth(_reset);
        Place(_reset, row with { left = right - resetWidth });
        var filterFrame = row with { right = right - resetWidth - Scale(10), left = right - resetWidth - Scale(10) - Scale(240) };
        PlaceTextBox(_filter, filterFrame, _palette.Window, Icon.Search);

        string category = SelectedCategory;
        string heading = category == AllCategories ? "All shortcuts" : category;
        var (on, total) = CountEnabled(category);
        string count = $"{on} of {total} on";
        int headingWidth = Paint.Measure(heading, _fonts.Heading).cx;
        _art.Add(hdc =>
        {
            Paint.Text(hdc, heading, _fonts.Heading, _palette.Text, row with { right = filterFrame.left - Scale(10) },
                DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
            Paint.Text(hdc, count, _fonts.Small, _palette.MutedText, row with { left = left + headingWidth + Scale(10), right = filterFrame.left - Scale(10) },
                DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
        });

        // The table, in a rounded panel.
        var panel = new RECT { left = left, top = row.bottom + Scale(14), right = right, bottom = client.bottom - Scale(20) };
        // The column headers' strip, whose colour shows in the panel's top corners.
        GetWindowRect(_shortcutList.Header, out var headerRect);
        PlaceInPanel(_shortcutList.Handle, panel, _palette.SurfaceAlt, headerRect.bottom - headerRect.top + 1);
        FitLastColumn();
    }

    /// <summary>Paints a rounded panel and fits <paramref name="hwnd"/> inside it, clipped to its rounded corners.</summary>
    void PlaceInPanel(IntPtr hwnd, RECT panel, uint? topFill = null, int topHeight = 0)
    {
        int radius = Scale(8);
        _art.Add(hdc => Paint.Panel(hdc, panel, radius, _palette.Surface, _palette.Border, _palette.Window, topFill, topHeight));
        var inner = Paint.Inflate(panel, -1);
        int width = inner.right - inner.left, height = inner.bottom - inner.top;
        Place(hwnd, inner);
        int diameter = 2 * (radius - 1);
        SetWindowRgn(hwnd, CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter), true);
    }

    void FitLastColumn()
    {
        GetClientRect(_shortcutList.Handle, out var r);
        int available = r.right - _shortcutList.GetColumnWidth(0) - _shortcutList.GetColumnWidth(1);
        if (available > 100) _shortcutList.SetColumnWidth(2, available);
    }
}
