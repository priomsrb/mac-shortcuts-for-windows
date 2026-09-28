using System.Runtime.InteropServices;

namespace MacShortcuts.Interop;

/// <summary>The common Open dialog.</summary>
internal static unsafe partial class ComDlg32
{
    public const uint OFN_NOCHANGEDIR = 0x8;
    public const uint OFN_PATHMUSTEXIST = 0x800;
    public const uint OFN_FILEMUSTEXIST = 0x1000;
    public const uint OFN_DONTADDTORECENT = 0x2000000;

    [StructLayout(LayoutKind.Sequential)]
    public struct OPENFILENAMEW
    {
        public uint lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public char* lpstrFilter;
        public char* lpstrCustomFilter;
        public uint nMaxCustFilter;
        public uint nFilterIndex;
        public char* lpstrFile;
        public uint nMaxFile;
        public char* lpstrFileTitle;
        public uint nMaxFileTitle;
        public char* lpstrInitialDir;
        public char* lpstrTitle;
        public uint Flags;
        public ushort nFileOffset;
        public ushort nFileExtension;
        public char* lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public char* lpTemplateName;
        public IntPtr pvReserved;
        public uint dwReserved;
        public uint FlagsEx;
    }

    [LibraryImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetOpenFileName(ref OPENFILENAMEW ofn);
}
