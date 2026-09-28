using System.Runtime.InteropServices;

namespace MacShortcuts.Interop;

internal static unsafe partial class Gdi32
{
    public const int TRANSPARENT = 1;
    public const int FW_NORMAL = 400;
    public const int FW_SEMIBOLD = 600;
    public const int FW_BOLD = 700;
    public const int PS_SOLID = 0;
    public const int PS_GEOMETRIC = 0x10000;
    public const int PS_ENDCAP_ROUND = 0x0;
    public const int PS_JOIN_ROUND = 0x0;
    public const int NULL_BRUSH = 5;
    public const int NULL_PEN = 8;
    public const int HALFTONE = 4;
    public const uint SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    public struct LOGBRUSH
    {
        public uint lbStyle;
        public uint lbColor;
        public nuint lbHatch;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
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

    /// <summary>A COLORREF (0x00BBGGRR).</summary>
    public static uint Rgb(byte r, byte g, byte b) => r | ((uint)g << 8) | ((uint)b << 16);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER header, uint usage, out IntPtr bits, IntPtr section, uint offset);

    [LibraryImport("gdi32.dll")]
    public static partial int GetDIBits(IntPtr hdc, IntPtr bitmap, uint start, uint lines, void* bits, void* info, uint usage);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateBitmap(int width, int height, uint planes, uint bitCount, IntPtr bits);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateCompatibleDC(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(IntPtr obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GdiFlush();

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateSolidBrush(uint color);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreatePen(int style, int width, uint color);

    [LibraryImport("gdi32.dll", EntryPoint = "CreateFontW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CreateFont(int height, int width, int escapement, int orientation, int weight,
        uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision,
        uint quality, uint pitchAndFamily, string faceName);

    [LibraryImport("gdi32.dll")]
    public static partial uint SetTextColor(IntPtr hdc, uint color);

    [LibraryImport("gdi32.dll")]
    public static partial uint SetBkColor(IntPtr hdc, uint color);

    [LibraryImport("gdi32.dll")]
    public static partial int SetBkMode(IntPtr hdc, int mode);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MoveToEx(IntPtr hdc, int x, int y, IntPtr previous);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool LineTo(IntPtr hdc, int x, int y);

    [LibraryImport("gdi32.dll", EntryPoint = "GetTextExtentPoint32W", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetTextExtentPoint32(IntPtr hdc, string text, int length, out SIZE size);

    [LibraryImport("gdi32.dll", EntryPoint = "GetTextFaceW")]
    public static partial int GetTextFace(IntPtr hdc, int count, char* faceName);

    [LibraryImport("gdi32.dll")]
    public static partial int SetTextCharacterExtra(IntPtr hdc, int extra);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(IntPtr hdc, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint rop);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool StretchBlt(IntPtr hdc, int x, int y, int width, int height,
        IntPtr source, int sourceX, int sourceY, int sourceWidth, int sourceHeight, uint rop);

    [LibraryImport("gdi32.dll")]
    public static partial int SetStretchBltMode(IntPtr hdc, int mode);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr previous);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr GetStockObject(int index);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr ExtCreatePen(int style, int width, in LOGBRUSH brush, int styleCount, IntPtr styles);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RoundRect(IntPtr hdc, int left, int top, int right, int bottom, int width, int height);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Ellipse(IntPtr hdc, int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Polyline(IntPtr hdc, POINT* points, int count);
}
