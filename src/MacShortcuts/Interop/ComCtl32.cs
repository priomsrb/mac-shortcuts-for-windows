using System.Runtime.InteropServices;

namespace MacShortcuts.Interop;

/// <summary>Common controls: ListView, custom draw and image lists.</summary>
internal static partial class ComCtl32
{
    public const uint ICC_LISTVIEW_CLASSES = 0x1;
    public const uint ICC_STANDARD_CLASSES = 0x4000;

    public const int LIM_SMALL = 0;
    public const int LIM_LARGE = 1;

    // Custom draw
    public const int NM_CLICK = -2;
    public const int NM_CUSTOMDRAW = -12;
    public const int CDDS_PREPAINT = 0x1;
    public const int CDDS_POSTPAINT = 0x2;
    public const int CDDS_ITEM = 0x10000;
    public const int CDDS_SUBITEM = 0x20000;
    public const int CDDS_ITEMPREPAINT = CDDS_ITEM | CDDS_PREPAINT;
    public const int CDDS_ITEMPOSTPAINT = CDDS_ITEM | CDDS_POSTPAINT;
    public const int CDDS_SUBITEMPREPAINT = CDDS_SUBITEM | CDDS_ITEMPREPAINT;
    public const int CDRF_DODEFAULT = 0x0;
    public const int CDRF_NEWFONT = 0x2;
    public const int CDRF_SKIPDEFAULT = 0x4;
    public const int CDRF_NOTIFYPOSTPAINT = 0x10;
    public const int CDRF_NOTIFYITEMDRAW = 0x20;
    public const int CDRF_NOTIFYSUBITEMDRAW = 0x20;
    public const uint CDIS_SELECTED = 0x1;
    public const uint CDIS_DISABLED = 0x4;
    public const uint CDIS_FOCUS = 0x10;
    public const uint CDIS_HOT = 0x40;
    public const uint CDIS_SHOWKEYBOARDCUES = 0x200;

    // ListView
    public const uint LVS_REPORT = 0x1;
    public const uint LVS_SHOWSELALWAYS = 0x8;
    public const uint LVS_SINGLESEL = 0x4;
    public const uint LVS_NOSORTHEADER = 0x8000;
    public const uint LVS_NOCOLUMNHEADER = 0x4000;
    public const int LVS_EX_CHECKBOXES = 0x4;
    public const int LVS_EX_FULLROWSELECT = 0x20;
    public const int LVS_EX_DOUBLEBUFFER = 0x10000;

    public const int LVM_FIRST = 0x1000;
    public const int LVM_SETBKCOLOR = LVM_FIRST + 1;
    public const int LVM_GETIMAGELIST = LVM_FIRST + 2;
    public const int LVM_SETIMAGELIST = LVM_FIRST + 3;
    public const int LVM_DELETEALLITEMS = LVM_FIRST + 9;
    public const int LVM_GETNEXTITEM = LVM_FIRST + 12;
    public const int LVM_GETITEMRECT = LVM_FIRST + 14;
    public const int LVM_SETITEMSTATE = LVM_FIRST + 43;
    public const int LVM_GETITEMSTATE = LVM_FIRST + 44;
    public const int LVM_GETHEADER = LVM_FIRST + 31;
    public const int LVM_GETCOLUMNWIDTH = LVM_FIRST + 29;
    public const int LVM_SETCOLUMNWIDTH = LVM_FIRST + 30;
    public const int LVM_SETTEXTCOLOR = LVM_FIRST + 36;
    public const int LVM_SETTEXTBKCOLOR = LVM_FIRST + 38;
    public const int LVM_SETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 54;
    public const int LVM_GETITEMCOUNT = LVM_FIRST + 4;
    public const int LVM_INSERTITEMW = LVM_FIRST + 77;
    public const int LVM_SETITEMTEXTW = LVM_FIRST + 116;
    public const int LVM_INSERTCOLUMNW = LVM_FIRST + 97;
    public const int LVM_INSERTGROUP = LVM_FIRST + 145;
    public const int LVM_GETGROUPINFO = LVM_FIRST + 149;
    public const int LVM_ENABLEGROUPVIEW = LVM_FIRST + 157;

    public const int LVN_FIRST = -100;
    public const int LVN_ITEMCHANGED = LVN_FIRST - 1;
    public const int LVN_KEYDOWN = LVN_FIRST - 55;
    public const int LVNI_SELECTED = 0x2;
    public const int LVIR_BOUNDS = 0;
    public const int LVIR_LABEL = 2;
    public const int LVSIL_SMALL = 1;
    public const uint ILC_COLOR32 = 0x20;

    // Header
    public const int HDM_GETITEMCOUNT = 0x1200;
    public const int HDM_GETITEMRECT = 0x1200 + 7;

    public const uint LVIF_TEXT = 0x1;
    public const uint LVIF_STATE = 0x8;
    public const uint LVIF_PARAM = 0x4;
    public const uint LVIF_GROUPID = 0x100;
    public const uint LVIS_STATEIMAGEMASK = 0xF000;
    public const uint LVCF_WIDTH = 0x2;
    public const uint LVCF_TEXT = 0x4;
    public const int LVCDI_ITEM = 0x0;
    public const int LVCDI_GROUP = 0x1;
    public const uint LVGF_HEADER = 0x1;
    public const uint LVGF_GROUPID = 0x10;
    public const int LVSIL_STATE = 2;

    const int WM_USER = 0x0400;

    /// <summary>The state image index for a ListView checkbox (1 = unchecked, 2 = checked).</summary>
    public static uint CheckState(bool isChecked) => (isChecked ? 2u : 1u) << 12;

    [StructLayout(LayoutKind.Sequential)]
    public struct INITCOMMONCONTROLSEX
    {
        public uint dwSize;
        public uint dwICC;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NMHDR
    {
        public IntPtr hwndFrom;
        public nuint idFrom;
        public int code;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NMCUSTOMDRAW
    {
        public NMHDR hdr;
        public int dwDrawStage;
        public IntPtr hdc;
        public RECT rc;
        public nuint dwItemSpec;
        public uint uItemState;
        public IntPtr lItemlParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NMLVCUSTOMDRAW
    {
        public NMCUSTOMDRAW nmcd;
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
    public struct NMLISTVIEW
    {
        public NMHDR hdr;
        public int iItem;
        public int iSubItem;
        public uint uNewState;
        public uint uOldState;
        public uint uChanged;
        public POINT ptAction;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NMITEMACTIVATE
    {
        public NMHDR hdr;
        public int iItem;
        public int iSubItem;
        public uint uNewState;
        public uint uOldState;
        public uint uChanged;
        public POINT ptAction;
        public IntPtr lParam;
        public uint uKeyFlags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NMLVKEYDOWN
    {
        public NMHDR hdr;
        public ushort wVKey;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LVCOLUMNW
    {
        public uint mask;
        public int fmt;
        public int cx;
        public IntPtr pszText;
        public int cchTextMax;
        public int iSubItem;
        public int iImage;
        public int iOrder;
        public int cxMin;
        public int cxDefault;
        public int cxIdeal;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LVITEMW
    {
        public uint mask;
        public int iItem;
        public int iSubItem;
        public uint state;
        public uint stateMask;
        public IntPtr pszText;
        public int cchTextMax;
        public int iImage;
        public IntPtr lParam;
        public int iIndent;
        public int iGroupId;
        public uint cColumns;
        public IntPtr puColumns;
        public IntPtr piColFmt;
        public int iGroup;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LVGROUP
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

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool InitCommonControlsEx(in INITCOMMONCONTROLSEX init);

    [LibraryImport("comctl32.dll")]
    public static partial int LoadIconMetric(IntPtr instance, IntPtr name, int metric, out IntPtr icon);

    [LibraryImport("comctl32.dll")]
    public static partial int LoadIconWithScaleDown(IntPtr instance, IntPtr name, int cx, int cy, out IntPtr icon);

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool SetWindowSubclass(IntPtr hwnd,
        delegate* unmanaged<IntPtr, uint, IntPtr, IntPtr, nuint, nuint, IntPtr> proc, nuint id, nuint refData);

    [LibraryImport("comctl32.dll")]
    public static partial IntPtr DefSubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ImageList_GetIconSize(IntPtr imageList, out int cx, out int cy);

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ImageList_Replace(IntPtr imageList, int index, IntPtr image, IntPtr mask);

    [LibraryImport("comctl32.dll")]
    public static partial IntPtr ImageList_Create(int cx, int cy, uint flags, int initial, int grow);

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ImageList_Destroy(IntPtr imageList);
}
