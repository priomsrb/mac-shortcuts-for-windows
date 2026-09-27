using System.Runtime.InteropServices;

namespace MacShortcuts;

/// <summary>
/// GroupBox whose frame is a subtle grey in dark mode; the default dark frame is near-white.
/// </summary>
internal sealed class ThemedGroupBox : GroupBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if (!Application.IsDarkModeEnabled)
        {
            base.OnPaint(e);
            return;
        }

        var g = e.Graphics;
        g.Clear(BackColor);
        var textSize = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        int textX = LogicalToDeviceUnits(8);
        int gap = LogicalToDeviceUnits(3);
        int frameTop = textSize.Height / 2;

        using (var pen = new Pen(Theming.DarkBorder))
        {
            int right = Width - 1, bottom = Height - 1;
            g.DrawLines(pen, [
                new Point(textX - gap, frameTop), new Point(0, frameTop), new Point(0, bottom),
                new Point(right, bottom), new Point(right, frameTop), new Point(textX + textSize.Width + gap, frameTop),
            ]);
        }
        TextRenderer.DrawText(g, Text, Font, new Point(textX, 0), ForeColor, TextFormatFlags.NoPadding);
    }
}

/// <summary>
/// ListView that matches dark mode: it paints its own group headers (the native control draws
/// them in dark blue regardless of theme) and can use dark checkboxes like the CheckBox controls.
/// </summary>
internal sealed class ThemedListView : ListView
{
    const int WM_REFLECT_NOTIFY = 0x2000 + 0x004E;
    const int NM_CUSTOMDRAW = -12;
    const int CDDS_PREPAINT = 0x1;
    const int CDRF_SKIPDEFAULT = 0x4;
    const int LVCDI_GROUP = 0x1;
    const int LVM_GETGROUPINFO = 0x1000 + 149;
    const int LVGF_HEADER = 0x1;
    const int LVM_GETIMAGELIST = 0x1000 + 2;
    const int LVSIL_STATE = 2;
    const int WM_THEMECHANGED = 0x031A;

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, nint wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct NMHDR
    {
        public IntPtr hwndFrom;
        public nuint idFrom;
        public int code;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NMLVCUSTOMDRAW
    {
        public NMHDR hdr;
        public int dwDrawStage;
        public IntPtr hdc;
        public RECT rc;
        public nuint dwItemSpec;
        public uint uItemState;
        public IntPtr lItemlParam;
        public int clrText;
        public int clrTextBk;
        public int iSubItem;
        public int dwItemType;
        public int clrFace;
        public int iIconEffect;
        public int iIconPhase;
        public int iPartId;
        public int iStateId;
        public RECT rcText;
        public uint uAlign;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LVGROUP
    {
        public uint cbSize;
        public uint mask;
        public IntPtr pszHeader;
        public int cchHeader;
        public IntPtr pszFooter;
        public int cchFooter;
        public int iGroupId;
        public uint stateMask;
        public uint state;
        public uint uAlign;
        public IntPtr pszSubtitle;
        public uint cchSubtitle;
        public IntPtr pszTask;
        public uint cchTask;
        public IntPtr pszDescriptionTop;
        public uint cchDescriptionTop;
        public IntPtr pszDescriptionBottom;
        public uint cchDescriptionBottom;
        public int iTitleImage;
        public int iExtendedImage;
        public int iFirstItem;
        public uint cItems;
        public IntPtr pszSubsetTitle;
        public uint cchSubsetTitle;
    }
}

/// <summary>Draws checkbox glyphs from the Windows dark theme into a native image list.</summary>
internal static class DarkCheckBoxes
{
    const int BP_CHECKBOX = 3;
    const int CBS_UNCHECKEDNORMAL = 1;
    const int CBS_CHECKEDNORMAL = 5;
    const int TS_DRAW = 2;

    /// <summary>Overwrites images 0 (unchecked) and 1 (checked), the ListView state image layout.</summary>
    /// <returns>false if this version of Windows has no dark theme.</returns>
    public static bool Draw(IntPtr imageList, int dpi)
    {
        IntPtr theme = OpenThemeDataForDpi(IntPtr.Zero, "DarkMode_Explorer::Button", dpi);
        if (theme == IntPtr.Zero) return false;
        try
        {
            ImageList_GetIconSize(imageList, out int width, out int height);
            Replace(imageList, 0, theme, CBS_UNCHECKEDNORMAL, width, height);
            Replace(imageList, 1, theme, CBS_CHECKEDNORMAL, width, height);
            return true;
        }
        finally { CloseThemeData(theme); }
    }

    static void Replace(IntPtr imageList, int index, IntPtr theme, int state, int width, int height)
    {
        // Draw into a zeroed 32-bit DIB so the theme's anti-aliased edges keep their alpha.
        var header = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height,
            biPlanes = 1,
            biBitCount = 32,
        };
        IntPtr dib = CreateDIBSection(IntPtr.Zero, ref header, 0, out _, IntPtr.Zero, 0);
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        IntPtr previous = SelectObject(dc, dib);
        try
        {
            GetThemePartSize(theme, IntPtr.Zero, BP_CHECKBOX, state, IntPtr.Zero, TS_DRAW, out var glyph);
            int w = Math.Min(glyph.cx, width), h = Math.Min(glyph.cy, height);
            var rect = new RECT { left = (width - w) / 2, top = (height - h) / 2 };
            rect.right = rect.left + w;
            rect.bottom = rect.top + h;
            DrawThemeBackground(theme, dc, BP_CHECKBOX, state, ref rect, IntPtr.Zero);
            GdiFlush();
        }
        finally
        {
            SelectObject(dc, previous);
            DeleteDC(dc);
        }
        ImageList_Replace(imageList, index, dib, IntPtr.Zero);
        DeleteObject(dib);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr OpenThemeDataForDpi(IntPtr hwnd, string classList, int dpi);

    [DllImport("uxtheme.dll")]
    static extern int CloseThemeData(IntPtr theme);

    [DllImport("uxtheme.dll")]
    static extern int GetThemePartSize(IntPtr theme, IntPtr hdc, int part, int state, IntPtr rect, int size, out SIZE result);

    [DllImport("uxtheme.dll")]
    static extern int DrawThemeBackground(IntPtr theme, IntPtr hdc, int part, int state, ref RECT rect, IntPtr clip);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER header, uint usage, out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    static extern bool GdiFlush();

    [DllImport("comctl32.dll")]
    static extern bool ImageList_GetIconSize(IntPtr imageList, out int cx, out int cy);

    [DllImport("comctl32.dll")]
    static extern bool ImageList_Replace(IntPtr imageList, int index, IntPtr image, IntPtr mask);
}
