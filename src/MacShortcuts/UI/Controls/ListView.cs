using System.Runtime.InteropServices;
using MacShortcuts.Interop;
using static MacShortcuts.Interop.ComCtl32;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI.Controls;

/// <summary>
/// A report-view ListView with checkboxes and groups. In dark mode it paints its own group headers
/// (the native control draws them in dark blue regardless of theme) and uses dark checkboxes.
/// </summary>
internal sealed unsafe class ListView
{
    public IntPtr Handle { get; }

    public ListView(IntPtr parent, uint exStyle)
    {
        Handle = CreateWindowEx(exStyle, "SysListView32", null,
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | LVS_REPORT | LVS_SINGLESEL | LVS_SHOWSELALWAYS | LVS_NOSORTHEADER,
            0, 0, 0, 0, parent, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        const int exStyles = LVS_EX_CHECKBOXES | LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER;
        SendMessage(Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, exStyles, exStyles);
        SendMessage(Handle, LVM_ENABLEGROUPVIEW, 1, IntPtr.Zero);
    }

    public IntPtr Header => SendMessage(Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);

    public int Count => (int)SendMessage(Handle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);

    public void AddColumn(string text, int width)
    {
        int index = ColumnCount++;
        fixed (char* p = text)
        {
            var column = new LVCOLUMNW { mask = LVCF_TEXT | LVCF_WIDTH, cx = width, pszText = (IntPtr)p };
            SendMessage(Handle, LVM_INSERTCOLUMNW, index, (IntPtr)(&column));
        }
    }

    public int ColumnCount { get; private set; }

    public int GetColumnWidth(int index) => (int)SendMessage(Handle, LVM_GETCOLUMNWIDTH, index, IntPtr.Zero);

    public void SetColumnWidth(int index, int width) => SendMessage(Handle, LVM_SETCOLUMNWIDTH, index, width);

    public void AddGroup(int id, string header)
    {
        fixed (char* p = header)
        {
            var group = new LVGROUP
            {
                cbSize = (uint)sizeof(LVGROUP),
                mask = LVGF_HEADER | LVGF_GROUPID,
                pszHeader = (IntPtr)p,
                iGroupId = id,
            };
            SendMessage(Handle, LVM_INSERTGROUP, -1, (IntPtr)(&group));
        }
    }

    /// <returns>The item's index; items keep the order they were added in.</returns>
    public int AddItem(int groupId, params ReadOnlySpan<string> columns)
    {
        int index;
        fixed (char* p = columns[0])
        {
            var item = new LVITEMW
            {
                mask = LVIF_TEXT | LVIF_GROUPID,
                iItem = Count,
                pszText = (IntPtr)p,
                iGroupId = groupId,
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

    public void SetColors(uint back, uint text)
    {
        SendMessage(Handle, LVM_SETBKCOLOR, IntPtr.Zero, (IntPtr)back);
        SendMessage(Handle, LVM_SETTEXTBKCOLOR, IntPtr.Zero, (IntPtr)back);
        SendMessage(Handle, LVM_SETTEXTCOLOR, IntPtr.Zero, (IntPtr)text);
    }

    /// <summary>
    /// Rebuilds the checkbox images at the current DPI. The item check states are lost,
    /// so the caller must set them again.
    /// </summary>
    public void RecreateCheckBoxes()
    {
        SendMessage(Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, LVS_EX_CHECKBOXES, 0);
        SendMessage(Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, LVS_EX_CHECKBOXES, LVS_EX_CHECKBOXES);
    }

    /// <summary>
    /// The native checkboxes keep the light theme in dark mode. Redraw them from the dark theme
    /// so they match the other checkboxes. The control owns its checkbox image list (and empties
    /// any list it's given instead), so the glyphs are overwritten in place.
    /// </summary>
    public void UseDarkCheckBoxes(int dpi)
    {
        IntPtr images = SendMessage(Handle, LVM_GETIMAGELIST, LVSIL_STATE, IntPtr.Zero);
        if (images != IntPtr.Zero && DarkCheckBoxes.Draw(images, dpi)) InvalidateRect(Handle, IntPtr.Zero, true);
    }

    /// <summary>
    /// The dark header theme keeps the light theme's black text. The header reports its drawing to
    /// the ListView (its parent), so intercept that to set the text colour.
    /// </summary>
    public void UseHeaderTextColor(uint color) =>
        SetWindowSubclass(Handle, &HeaderColorSubclass, 1, color);

    [UnmanagedCallersOnly]
    static IntPtr HeaderColorSubclass(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, nuint color)
    {
        if (msg == WM_NOTIFY && ((NMHDR*)lParam)->code == NM_CUSTOMDRAW)
        {
            var cd = (NMCUSTOMDRAW*)lParam;
            if (cd->hdr.hwndFrom == SendMessage(hwnd, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero))
            {
                if (cd->dwDrawStage == CDDS_PREPAINT) return CDRF_NOTIFYITEMDRAW;
                if (cd->dwDrawStage == CDDS_ITEMPREPAINT)
                {
                    _ = SetTextColor(cd->hdc, (uint)color);
                    return CDRF_DODEFAULT;
                }
            }
        }
        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    /// <summary>Handles NM_CUSTOMDRAW in dark mode, painting the group headers.</summary>
    public IntPtr CustomDraw(NMLVCUSTOMDRAW* cd, Palette palette, int dpi)
    {
        if (cd->nmcd.dwDrawStage != CDDS_PREPAINT) return CDRF_DODEFAULT;
        if (cd->dwItemType != LVCDI_GROUP) return CDRF_NOTIFYITEMDRAW;
        DrawGroupHeader(cd->nmcd.hdc, (int)cd->nmcd.dwItemSpec, cd->rcText, palette, dpi);
        return CDRF_SKIPDEFAULT;
    }

    void DrawGroupHeader(IntPtr hdc, int groupId, RECT bounds, Palette palette, int dpi)
    {
        int Scale(int value) => value * dpi / 96;

        IntPtr back = CreateSolidBrush(palette.Control);
        FillRect(hdc, bounds, back);
        DeleteObject(back);

        const int WM_GETFONT = 0x0031;
        IntPtr oldFont = SelectObject(hdc, SendMessage(Handle, WM_GETFONT, IntPtr.Zero, IntPtr.Zero));
        string text = GetGroupHeader(groupId);
        var textRect = bounds with { left = bounds.left + Scale(6) };
        DrawText(hdc, text, text.Length, ref textRect, DT_CALCRECT | DT_SINGLELINE | DT_NOPREFIX);
        textRect.top = bounds.top;
        textRect.bottom = bounds.bottom;
        _ = SetBkMode(hdc, TRANSPARENT);
        _ = SetTextColor(hdc, palette.Accent);
        DrawText(hdc, text, text.Length, ref textRect, DT_SINGLELINE | DT_VCENTER | DT_NOPREFIX);
        SelectObject(hdc, oldFont);

        int lineX = textRect.right + Scale(6);
        int lineY = bounds.top + (bounds.bottom - bounds.top) / 2;
        if (lineX < bounds.right)
        {
            IntPtr pen = CreatePen(PS_SOLID, 1, palette.Border);
            IntPtr oldPen = SelectObject(hdc, pen);
            MoveToEx(hdc, lineX, lineY, IntPtr.Zero);
            LineTo(hdc, bounds.right - Scale(8), lineY);
            SelectObject(hdc, oldPen);
            DeleteObject(pen);
        }
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
}
