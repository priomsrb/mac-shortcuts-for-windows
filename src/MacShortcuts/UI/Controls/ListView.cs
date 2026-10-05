using System.Runtime.InteropServices;
using MacShortcuts.Interop;
using MacShortcuts.UI.Drawing;
using static MacShortcuts.Interop.ComCtl32;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI.Controls;

/// <summary>
/// A report-view ListView. Its column headers, group headers and checkboxes are drawn in the
/// window's palette; the owner draws the rows through NM_CUSTOMDRAW.
/// </summary>
internal sealed unsafe class ListView
{
    readonly List<string> _columns = [];
    Palette? _palette;
    IntPtr _headerFont;
    int _headerPadding;
    int _dpi = 96;

    /// <param name="checkBoxes">A checkbox on each row.</param>
    /// <param name="header">Column headers; without them the list is a plain list of rows.</param>
    public ListView(IntPtr parent, bool checkBoxes, bool header)
    {
        Handle = CreateWindowEx(0, "SysListView32", null,
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | LVS_REPORT | LVS_SINGLESEL | LVS_SHOWSELALWAYS | LVS_NOSORTHEADER
                | (header ? 0 : LVS_NOCOLUMNHEADER),
            0, 0, 0, 0, parent, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        int exStyles = LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER | (checkBoxes ? LVS_EX_CHECKBOXES : 0);
        SendMessage(Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, exStyles, exStyles);

        // Draws the column headers, which report their drawing to the ListView (their parent).
        var self = GCHandle.Alloc(this);
        SetWindowSubclass(Handle, &Subclass, 1, (nuint)GCHandle.ToIntPtr(self));
        // Makes the column headers taller than their font alone would.
        if (header) SetWindowSubclass(Header, &HeaderSubclass, 1, (nuint)GCHandle.ToIntPtr(self));
    }

    public IntPtr Handle { get; }

    /// <summary>
    /// Pixels before each new row's checkbox. Indents are counted in small images, which are
    /// 1 pixel wide once <see cref="ApplyStyle"/> has run.
    /// </summary>
    public int ItemIndent { get; set; }

    /// <summary>
    /// Pixels of empty space before each checkbox, inside its image so that the row's highlight
    /// includes it. Takes effect in <see cref="DrawCheckBoxes"/>.
    /// </summary>
    public int CheckBoxPadding { get; set; }

    public IntPtr Header => SendMessage(Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);

    public int Count => (int)SendMessage(Handle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);

    /// <summary>The selected row, or -1.</summary>
    public int SelectedIndex => (int)SendMessage(Handle, LVM_GETNEXTITEM, -1, LVNI_SELECTED);

    public void AddColumn(string text, int width)
    {
        int index = _columns.Count;
        _columns.Add(text);
        fixed (char* p = text)
        {
            var column = new LVCOLUMNW { mask = LVCF_TEXT | LVCF_WIDTH, cx = width, pszText = (IntPtr)p };
            SendMessage(Handle, LVM_INSERTCOLUMNW, index, (IntPtr)(&column));
        }
    }

    public int GetColumnWidth(int index) => (int)SendMessage(Handle, LVM_GETCOLUMNWIDTH, index, IntPtr.Zero);

    public void SetColumnWidth(int index, int width) => SendMessage(Handle, LVM_SETCOLUMNWIDTH, index, width);

    public void AddGroup(int id, string header)
    {
        // A blank subtitle makes the header a line taller, which DrawGroupHeader uses as padding.
        fixed (char* p = header)
        fixed (char* subtitle = " ")
        {
            var group = new LVGROUP
            {
                cbSize = (uint)sizeof(LVGROUP),
                mask = LVGF_HEADER | LVGF_SUBTITLE | LVGF_GROUPID,
                pszHeader = (IntPtr)p,
                pszSubtitle = (IntPtr)subtitle,
                iGroupId = id,
            };
            SendMessage(Handle, LVM_INSERTGROUP, -1, (IntPtr)(&group));
        }
    }

    public void EnableGroups(bool enable) => SendMessage(Handle, LVM_ENABLEGROUPVIEW, enable ? 1 : 0, IntPtr.Zero);

    public void Clear() => SendMessage(Handle, LVM_DELETEALLITEMS, IntPtr.Zero, IntPtr.Zero);

    /// <param name="groupId">The group added with <see cref="AddGroup"/>, or 0 for none.</param>
    /// <returns>The item's index; items keep the order they were added in.</returns>
    public int AddItem(int groupId, params ReadOnlySpan<string> columns)
    {
        int index;
        fixed (char* p = columns[0])
        {
            var item = new LVITEMW
            {
                // Inserting fails with a group that doesn't exist.
                mask = LVIF_TEXT | LVIF_INDENT | (groupId > 0 ? LVIF_GROUPID : 0),
                iItem = Count,
                pszText = (IntPtr)p,
                iGroupId = groupId,
                iIndent = ItemIndent,
            };
            index = (int)SendMessage(Handle, LVM_INSERTITEMW, IntPtr.Zero, (IntPtr)(&item));
        }
        for (int i = 1; i < columns.Length; i++)
        {
            fixed (char* p = columns[i])
            {
                var sub = new LVITEMW { iSubItem = i, pszText = (IntPtr)p };
                SendMessage(Handle, LVM_SETITEMTEXTW, index, (IntPtr)(&sub));
            }
        }
        return index;
    }

    public void Select(int index)
    {
        const uint LVIS_FOCUSED = 0x1, LVIS_SELECTED = 0x2;
        var item = new LVITEMW { stateMask = LVIS_FOCUSED | LVIS_SELECTED, state = LVIS_FOCUSED | LVIS_SELECTED };
        SendMessage(Handle, LVM_SETITEMSTATE, index, (IntPtr)(&item));
    }

    public RECT GetItemRect(int index, int part = LVIR_BOUNDS)
    {
        var rect = new RECT { left = part };
        SendMessage(Handle, LVM_GETITEMRECT, index, (IntPtr)(&rect));
        return rect;
    }

    /// <summary>Whether a row is selected. Custom draw's CDIS_SELECTED isn't reliable for this.</summary>
    public bool IsSelected(int index)
    {
        const uint LVIS_SELECTED = 0x2;
        return ((uint)SendMessage(Handle, LVM_GETITEMSTATE, index, (IntPtr)LVIS_SELECTED) & LVIS_SELECTED) != 0;
    }

    public bool IsChecked(int index) =>
        ((uint)SendMessage(Handle, LVM_GETITEMSTATE, index, (IntPtr)LVIS_STATEIMAGEMASK) & LVIS_STATEIMAGEMASK) == CheckState(true);

    public void SetChecked(int index, bool isChecked)
    {
        var item = new LVITEMW { stateMask = LVIS_STATEIMAGEMASK, state = CheckState(isChecked) };
        SendMessage(Handle, LVM_SETITEMSTATE, index, (IntPtr)(&item));
    }

    /// <summary>Whether a LVN_ITEMCHANGED notification is the user toggling a checkbox.</summary>
    public static bool IsCheckToggle(NMLISTVIEW* change) =>
        (change->uChanged & LVIF_STATE) != 0
        && (change->uOldState & LVIS_STATEIMAGEMASK) != 0 // 0 = the item was just added
        && ((change->uNewState ^ change->uOldState) & LVIS_STATEIMAGEMASK) != 0;

    /// <summary>Colours the list, its headers and checkboxes. Call again after the DPI changes.</summary>
    /// <param name="headerPadding">Extra space above and below the column headers' titles.</param>
    /// <param name="groupGap">Space after each group's last row.</param>
    public void ApplyStyle(Palette palette, IntPtr font, IntPtr headerFont, int rowHeight, int dpi, int headerPadding = 0, int groupGap = 0)
    {
        _palette = palette;
        _headerFont = headerFont;
        _headerPadding = headerPadding;
        _dpi = dpi;
        SendMessage(Handle, WM_SETFONT, font, 1);
        SendMessage(Handle, LVM_SETBKCOLOR, IntPtr.Zero, (IntPtr)palette.Surface);
        SendMessage(Handle, LVM_SETTEXTBKCOLOR, IntPtr.Zero, (IntPtr)palette.Surface);
        SendMessage(Handle, LVM_SETTEXTCOLOR, IntPtr.Zero, (IntPtr)palette.Text);

        // Rows are as tall as the tallest image, so a 1-pixel-wide image sets their height.
        // The list destroys the image list it has when it's destroyed, but not one it's replacing.
        IntPtr old = SendMessage(Handle, LVM_SETIMAGELIST, LVSIL_SMALL, ImageList_Create(1, rowHeight, ILC_COLOR32, 1, 0));
        if (old != IntPtr.Zero) ImageList_Destroy(old);

        var metrics = new LVGROUPMETRICS { cbSize = (uint)sizeof(LVGROUPMETRICS), mask = LVGMF_BORDERSIZE, Bottom = (uint)groupGap };
        SendMessage(Handle, LVM_SETGROUPMETRICS, IntPtr.Zero, (IntPtr)(&metrics));

        DrawCheckBoxes();
    }

    /// <summary>
    /// Rebuilds the checkbox images at the current DPI. The item check states are lost,
    /// so the caller must set them again.
    /// </summary>
    public void RecreateCheckBoxes()
    {
        SendMessage(Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, LVS_EX_CHECKBOXES, 0);
        SendMessage(Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, LVS_EX_CHECKBOXES, LVS_EX_CHECKBOXES);
        DrawCheckBoxes();
    }

    /// <summary>
    /// Replaces the native checkbox glyphs with the window's. The control owns its checkbox image
    /// list (and empties any list it's given instead), so the glyphs are overwritten in place.
    /// </summary>
    public void DrawCheckBoxes()
    {
        IntPtr images = SendMessage(Handle, LVM_GETIMAGELIST, LVSIL_STATE, IntPtr.Zero);
        if (images == IntPtr.Zero || _palette is not { } palette) return;
        ImageList_GetIconSize(images, out int width, out int height);
        // The images are square until padded; resizing the list clears it, so the glyphs are drawn again.
        int padding = CheckBoxPadding;
        if (width != height + padding)
        {
            // The list only takes a new image size from a new image list.
            width = height + padding;
            IntPtr old = SendMessage(Handle, LVM_SETIMAGELIST, LVSIL_STATE, ImageList_Create(width, height, ILC_COLOR32, 2, 0));
            if (old != IntPtr.Zero && old != images) ImageList_Destroy(old);
            images = SendMessage(Handle, LVM_GETIMAGELIST, LVSIL_STATE, IntPtr.Zero);
            ImageList_SetImageCount(images, 2);
        }
        float size = MathF.Min(16f * _dpi / 96, height);
        for (int i = 0; i < 2; i++)
        {
            bool isChecked = i == 1;
            IntPtr bitmap = Canvas.RenderBitmap(width, height,
                c => Glyphs.CheckBox(c, padding + (height - size) / 2, (height - size) / 2, size, isChecked, palette));
            ImageList_Replace(images, i, bitmap, IntPtr.Zero);
            DeleteObject(bitmap);
        }
        InvalidateRect(Handle, IntPtr.Zero, true);
    }

    /// <summary>Paints a group header: its title, centred on a tinted band.</summary>
    public void DrawGroupHeader(IntPtr hdc, int groupId, RECT bounds, Palette palette, IntPtr font)
    {
        int Scale(int value) => value * _dpi / 96;

        Paint.Fill(hdc, bounds, palette.SurfaceAlt);
        Paint.HorizontalLine(hdc, bounds.left, bounds.right, bounds.top, palette.Divider);
        Paint.HorizontalLine(hdc, bounds.left, bounds.right, bounds.bottom - 1, palette.Divider);
        Paint.Text(hdc, GetGroupHeader(groupId), font, palette.Text, bounds with { left = bounds.left + Scale(12) });
    }

    /// <summary>
    /// Fills the space between one group's last row and the next group's header, where the
    /// Explorer theme draws column lines.
    /// </summary>
    public void FillGroupGaps(IntPtr hdc, uint color)
    {
        for (int i = 1; i < Count; i++)
        {
            int group = GetItemGroup(i);
            if (group == GetItemGroup(i - 1)) continue;
            int above = GetItemRect(i - 1).bottom;
            var header = GetGroupHeaderRect(group);
            if (header.top > above) Paint.Fill(hdc, header with { top = above, bottom = header.top }, color);
        }
    }

    int GetItemGroup(int index)
    {
        var item = new LVITEMW { mask = LVIF_GROUPID, iItem = index };
        SendMessage(Handle, LVM_GETITEMW, IntPtr.Zero, (IntPtr)(&item));
        return item.iGroupId;
    }

    RECT GetGroupHeaderRect(int groupId)
    {
        var rect = new RECT { top = LVGGR_HEADER };
        SendMessage(Handle, LVM_GETGROUPRECT, groupId, (IntPtr)(&rect));
        return rect;
    }

    string GetGroupHeader(int groupId)
    {
        const int capacity = 256;
        char* buffer = stackalloc char[capacity];
        var group = new LVGROUP
        {
            cbSize = (uint)sizeof(LVGROUP),
            mask = LVGF_HEADER,
            pszHeader = (IntPtr)buffer,
            cchHeader = capacity,
        };
        if (SendMessage(Handle, LVM_GETGROUPINFO, groupId, (IntPtr)(&group)) == -1) return "";
        return new string(buffer);
    }

    IntPtr DrawHeader(NMCUSTOMDRAW* cd)
    {
        if (_palette is not { } palette) return CDRF_DODEFAULT;
        int Scale(int value) => value * _dpi / 96;

        switch (cd->dwDrawStage)
        {
            case CDDS_PREPAINT:
                Paint.Fill(cd->hdc, cd->rc, palette.SurfaceAlt);
                return CDRF_NOTIFYITEMDRAW | CDRF_NOTIFYPOSTPAINT;

            case CDDS_ITEMPREPAINT:
                int column = (int)cd->dwItemSpec;
                Paint.Fill(cd->hdc, cd->rc, palette.SurfaceAlt);
                // Line each title up with its column's text: in the first column, that's after the checkbox.
                int indent = column == 0 && Count > 0 ? GetItemRect(0, LVIR_LABEL).left + Scale(1) : Scale(3);
                var textRect = cd->rc with { left = cd->rc.left + indent, right = cd->rc.right - Scale(4) };
                if (column < _columns.Count)
                    Paint.Text(cd->hdc, _columns[column], _headerFont, palette.MutedText, textRect,
                        DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
                return CDRF_SKIPDEFAULT;

            case CDDS_POSTPAINT:
                GetClientRect(cd->hdr.hwndFrom, out var client);
                Paint.HorizontalLine(cd->hdc, client.left, client.right, client.bottom - 1, palette.Border);
                return CDRF_DODEFAULT;
        }
        return CDRF_DODEFAULT;
    }

    [UnmanagedCallersOnly]
    static IntPtr Subclass(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, nuint self)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)self);
        var list = (ListView)handle.Target!;
        switch (msg)
        {
            case WM_NOTIFY when ((NMHDR*)lParam)->code == NM_CUSTOMDRAW
                                && ((NMHDR*)lParam)->hwndFrom == SendMessage(hwnd, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero):
                return list.DrawHeader((NMCUSTOMDRAW*)lParam);

            case WM_NCDESTROY:
                handle.Free();
                break;
        }
        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    /// <summary>The column headers' subclass. The ListView frees the shared handle; the headers go first.</summary>
    [UnmanagedCallersOnly]
    static IntPtr HeaderSubclass(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, nuint self)
    {
        IntPtr result = DefSubclassProc(hwnd, msg, wParam, lParam);
        if (msg == HDM_LAYOUT && result != 0)
        {
            var list = (ListView)GCHandle.FromIntPtr((IntPtr)self).Target!;
            var layout = (HDLAYOUT*)lParam;
            layout->pwpos->cy += 2 * list._headerPadding;
            layout->prc->top += 2 * list._headerPadding;
        }
        return result;
    }
}
