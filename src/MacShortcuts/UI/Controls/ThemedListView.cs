using System.Runtime.InteropServices;
using MacShortcuts.Interop;
using static MacShortcuts.Interop.ComCtl32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI.Controls;

/// <summary>
/// ListView that matches dark mode: it paints its own group headers (the native control draws
/// them in dark blue regardless of theme) and can use dark checkboxes like the CheckBox controls.
/// </summary>
internal sealed class ThemedListView : ListView
{
    // WinForms reflects WM_NOTIFY back to the control that sent it.
    const int WM_REFLECT_NOTIFY = 0x2000 + WM_NOTIFY;

    bool _darkCheckBoxes;

    /// <summary>
    /// The native checkboxes keep the light theme in dark mode, unlike CheckBox controls.
    /// Redraw them from the dark theme so both look the same.
    /// </summary>
    public void UseDarkCheckBoxes()
    {
        _darkCheckBoxes = true;
        if (IsHandleCreated) RedrawCheckBoxes();
    }

    // The control owns its checkbox image list (and empties any list it's given instead),
    // so overwrite the glyphs in place.
    void RedrawCheckBoxes()
    {
        IntPtr images = SendMessage(Handle, LVM_GETIMAGELIST, LVSIL_STATE, IntPtr.Zero);
        if (images != IntPtr.Zero && DarkCheckBoxes.Draw(images, DeviceDpi)) Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_darkCheckBoxes) RedrawCheckBoxes();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        if (_darkCheckBoxes) RedrawCheckBoxes();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_REFLECT_NOTIFY && Application.IsDarkModeEnabled
            && Marshal.ReadInt32(m.LParam, 2 * IntPtr.Size) == NM_CUSTOMDRAW)
        {
            var cd = Marshal.PtrToStructure<NMLVCUSTOMDRAW>(m.LParam);
            if (cd.dwDrawStage == CDDS_PREPAINT && cd.dwItemType == LVCDI_GROUP)
            {
                DrawGroupHeader(cd.hdc, (int)cd.dwItemSpec, cd.rcText);
                m.Result = CDRF_SKIPDEFAULT;
                return;
            }
        }
        base.WndProc(ref m);

        // The control rebuilds its checkbox images whenever its theme changes.
        if (m.Msg == WM_THEMECHANGED && _darkCheckBoxes) RedrawCheckBoxes();
    }

    void DrawGroupHeader(IntPtr hdc, int groupId, RECT rc)
    {
        using var g = Graphics.FromHdc(hdc);
        var bounds = Rectangle.FromLTRB(rc.left, rc.top, rc.right, rc.bottom);
        using (var back = new SolidBrush(BackColor)) g.FillRectangle(back, bounds);

        string text = GetGroupHeader(groupId);
        int x = bounds.Left + LogicalToDeviceUnits(6);
        var size = TextRenderer.MeasureText(g, text, Font, bounds.Size, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, text, Font, new Rectangle(x, bounds.Top, size.Width, bounds.Height), Theming.DarkAccentText,
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        int lineX = x + size.Width + LogicalToDeviceUnits(6);
        int lineY = bounds.Top + bounds.Height / 2;
        if (lineX < bounds.Right)
        {
            using var pen = new Pen(Theming.DarkBorder);
            g.DrawLine(pen, lineX, lineY, bounds.Right - LogicalToDeviceUnits(8), lineY);
        }
    }

    string GetGroupHeader(int groupId)
    {
        const int capacity = 256;
        IntPtr buffer = Marshal.AllocHGlobal(capacity * sizeof(char));
        try
        {
            var group = new LVGROUP
            {
                cbSize = (uint)Marshal.SizeOf<LVGROUP>(),
                mask = LVGF_HEADER,
                pszHeader = buffer,
                cchHeader = capacity,
            };
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<LVGROUP>());
            try
            {
                Marshal.StructureToPtr(group, ptr, false);
                if (SendMessage(Handle, LVM_GETGROUPINFO, groupId, ptr) == -1) return "";
                return Marshal.PtrToStringUni(buffer) ?? "";
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
}
