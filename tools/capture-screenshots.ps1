# Regenerates docs/images/{shortcuts,excluded,settings}-{light,dark}.png from the Debug build.
# See tools/SCREENSHOTS.md. Run from PowerShell (not elevated); keep hands off the mouse while it runs.
# It temporarily replaces %APPDATA%\MacShortcuts\settings.json and restores it afterwards.
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\docs\images'),
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\MacShortcuts\bin\Debug\net10.0-windows\MacShortcuts.exe'),
    [int]$WindowWidth = 2000   # physical px; wide enough that no list column truncates
)
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class W {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr c);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int hgt, uint f);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int l, t, r, b; }
}
'@
# Per-monitor aware, like the app, so coordinates and sizes are physical pixels.
[void][W]::SetProcessDpiAwarenessContext([IntPtr]::new(-4))

$Exe = (Resolve-Path $Exe).Path
$OutDir = (Resolve-Path $OutDir).Path
$settingsPath = "$env:APPDATA\MacShortcuts\settings.json"
$backup = if (Test-Path $settingsPath) { Get-Content $settingsPath -Raw } else { $null }

function Click($x, $y) {
    [void][W]::SetCursorPos($x, $y); Start-Sleep -Milliseconds 100
    [W]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); [W]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 100
}

try {
    foreach ($theme in 'Light', 'Dark') {
        Stop-Process -Name MacShortcuts -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
        @{ Enabled = $true; UseLeftAlt = $true; UseRightAlt = $true; Theme = $theme; Shortcuts = @{}; ExcludedApps = @('mstsc.exe', 'vmconnect.exe') } |
            ConvertTo-Json | Set-Content $settingsPath -Encoding utf8
        $p = Start-Process $Exe -PassThru
        $h = [IntPtr]::Zero
        for ($i = 0; $i -lt 50 -and $h -eq [IntPtr]::Zero; $i++) { Start-Sleep -Milliseconds 200; $p.Refresh(); $h = $p.MainWindowHandle }
        if ($h -eq [IntPtr]::Zero) { throw 'The app showed no window.' }
        [void][W]::ShowWindow($h, 9)
        # The height is clamped by the app/screen; the width is honoured above its minimum.
        [void][W]::SetWindowPos($h, [IntPtr]::Zero, 100, 50, $WindowWidth, 1219, 0x44)
        [void][W]::SetForegroundWindow($h)
        Start-Sleep -Seconds 1
        $r = New-Object W+RECT; [void][W]::GetWindowRect($h, [ref]$r)
        # Visible frame without the invisible resize borders.
        $f = New-Object W+RECT; [void][W]::DwmGetWindowAttribute($h, 9, [ref]$f, 16)
        $crop = New-Object System.Drawing.Rectangle ($f.l - $r.l), ($f.t - $r.t), ($f.r - $f.l), ($f.b - $f.t)

        # Rail icon offsets from the window's top-left, in physical px at the scale the window opens at.
        foreach ($page in @(@('shortcuts', 215), @('excluded', 325), @('settings', 437))) {
            Click ($r.l + 78) ($r.t + $page[1]); Start-Sleep -Milliseconds 700
            $bmp = New-Object System.Drawing.Bitmap ($r.r - $r.l), ($r.b - $r.t)
            $g = [System.Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc()
            [void][W]::PrintWindow($h, $dc, 2)   # 2 = PW_RENDERFULLCONTENT
            $g.ReleaseHdc($dc); $g.Dispose()
            # Keep native resolution: no resampling, so the page can show it at half size on high-DPI screens.
            $img = $bmp.Clone($crop, $bmp.PixelFormat)
            $img.Save((Join-Path $OutDir "$($page[0])-$($theme.ToLower()).png"), [System.Drawing.Imaging.ImageFormat]::Png)
            "$($page[0])-$($theme.ToLower()): $($img.Width) x $($img.Height)"
            $img.Dispose(); $bmp.Dispose()
        }
    }
}
finally {
    Stop-Process -Name MacShortcuts -Force -ErrorAction SilentlyContinue
    if ($null -ne $backup) { Set-Content $settingsPath $backup -Encoding utf8 -NoNewline }
}
