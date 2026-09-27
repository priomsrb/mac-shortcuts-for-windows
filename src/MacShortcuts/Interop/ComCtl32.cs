using System.Runtime.InteropServices;

namespace MacShortcuts.Interop;

/// <summary>Common controls: ListView messages and image lists.</summary>
internal static partial class ComCtl32
{
    public const int NM_CUSTOMDRAW = -12;
    public const int CDDS_PREPAINT = 0x1;
    public const int CDRF_SKIPDEFAULT = 0x4;
    public const int LVCDI_GROUP = 0x1;
    public const int LVM_GETIMAGELIST = 0x1000 + 2;
    public const int LVM_GETGROUPINFO = 0x1000 + 149;
    public const int LVGF_HEADER = 0x1;
    public const int LVSIL_STATE = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct NMHDR
    {
        public IntPtr hwndFrom;
        public nuint idFrom;
        public int code;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NMLVCUSTOMDRAW
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
    public static partial bool ImageList_GetIconSize(IntPtr imageList, out int cx, out int cy);

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ImageList_Replace(IntPtr imageList, int index, IntPtr image, IntPtr mask);
}
