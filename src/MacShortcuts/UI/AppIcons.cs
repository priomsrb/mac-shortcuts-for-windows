using static MacShortcuts.Interop.ComCtl32;
using static MacShortcuts.Interop.Gdi32;
using static MacShortcuts.Interop.Kernel32;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI;

/// <summary>The app icon (embedded from app.ico), loaded at the system's small and large icon sizes.</summary>
internal static unsafe class AppIcons
{
    // The compiler embeds <ApplicationIcon> under this resource ID.
    const int AppIconId = 32512;

    public static IntPtr Small { get; } = Load(LIM_SMALL);
    public static IntPtr Large { get; } = Load(LIM_LARGE);
    public static IntPtr SmallDisabled { get; } = CreateDisabled(Small);

    /// <summary>The UAC shield as a 32-bit bitmap for a menu item, or zero. The caller deletes it.</summary>
    public static IntPtr CreateShieldBitmap(uint dpi)
    {
        const int IDI_SHIELD = 32518, SM_CXSMICON = 49;
        int size = GetSystemMetricsForDpi(SM_CXSMICON, dpi);
        if (LoadIconWithScaleDown(IntPtr.Zero, IDI_SHIELD, size, size, out IntPtr icon) < 0) return IntPtr.Zero;

        // Menus take premultiplied alpha, which DrawIconEx produces on a zeroed 32-bit bitmap.
        var header = new BITMAPINFOHEADER { biSize = sizeof(BITMAPINFOHEADER), biWidth = size, biHeight = -size, biPlanes = 1, biBitCount = 32 };
        IntPtr bitmap = CreateDIBSection(IntPtr.Zero, ref header, 0, out _, IntPtr.Zero, 0);
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        IntPtr old = SelectObject(dc, bitmap);
        DrawIconEx(dc, 0, 0, icon, size, size, 0, IntPtr.Zero, DI_NORMAL);
        SelectObject(dc, old);
        DeleteDC(dc);
        DestroyIcon(icon);
        return bitmap;
    }

    static IntPtr Load(int metric)
    {
        if (LoadIconMetric(GetModuleHandle(null), AppIconId, metric, out IntPtr icon) >= 0) return icon;
        // IDI_APPLICATION, the generic app icon.
        return LoadIconMetric(IntPtr.Zero, 32512, metric, out icon) >= 0 ? icon : IntPtr.Zero;
    }

    /// <summary>A greyed-out, semi-transparent copy of <paramref name="icon"/>.</summary>
    static IntPtr CreateDisabled(IntPtr icon)
    {
        if (!GetIconInfo(icon, out var info)) return icon;
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        try
        {
            // Room for the header and the largest possible colour table.
            byte* buffer = stackalloc byte[sizeof(BITMAPINFOHEADER) + 256 * 4];
            var header = (BITMAPINFOHEADER*)buffer;
            header->biSize = sizeof(BITMAPINFOHEADER);
            if (GetDIBits(dc, info.hbmColor, 0, 0, null, buffer, 0) == 0) return icon;

            int width = header->biWidth, height = Math.Abs(header->biHeight);
            *header = new BITMAPINFOHEADER
            {
                biSize = sizeof(BITMAPINFOHEADER),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
            };
            var pixels = new uint[width * height];
            fixed (uint* p = pixels)
            {
                if (GetDIBits(dc, info.hbmColor, 0, (uint)height, p, buffer, 0) == 0) return icon;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                uint px = pixels[i];
                uint b = px & 0xFF, g = (px >> 8) & 0xFF, r = (px >> 16) & 0xFF, a = px >> 24;
                uint gray = (r * 30 + g * 59 + b * 11) / 100;
                pixels[i] = (a * 55 / 100 << 24) | (gray << 16) | (gray << 8) | gray;
            }

            IntPtr color = CreateDIBSection(IntPtr.Zero, ref *header, 0, out IntPtr bits, IntPtr.Zero, 0);
            fixed (uint* p = pixels) Buffer.MemoryCopy(p, (void*)bits, pixels.Length * 4L, pixels.Length * 4L);

            // An all-zero mask: the alpha channel decides what's transparent.
            var maskBits = new byte[(width + 15) / 16 * 2 * height];
            IntPtr mask;
            fixed (byte* m = maskBits) mask = CreateBitmap(width, height, 1, 1, (IntPtr)m);

            var disabled = CreateIconIndirect(new ICONINFO { fIcon = 1, hbmColor = color, hbmMask = mask });
            DeleteObject(color);
            DeleteObject(mask);
            return disabled == IntPtr.Zero ? icon : disabled;
        }
        finally
        {
            DeleteDC(dc);
            DeleteObject(info.hbmColor);
            DeleteObject(info.hbmMask);
        }
    }
}
