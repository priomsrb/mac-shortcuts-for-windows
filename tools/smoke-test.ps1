# Smoke test: with MacShortcuts running, opens a test window and simulates Mac-style
# shortcuts as if typed on the keyboard, checking the results.
# Keep your hands off the keyboard/mouse while it runs (a few seconds).
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Kb {
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public KI ki; public long pad; }
    [StructLayout(LayoutKind.Sequential)] struct KI { public ushort vk; public ushort scan; public uint flags; public uint time; public IntPtr extra; }
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] i, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint c, uint t);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    static bool Ext(int vk) { return (vk >= 0x21 && vk <= 0x28) || vk == 0xA5; }
    public static void Key(int vk, bool up) {
        var i = new INPUT[1];
        i[0].type = 1; i[0].ki.vk = (ushort)vk; i[0].ki.scan = (ushort)MapVirtualKey((uint)vk, 0);
        i[0].ki.flags = (up ? 2u : 0u) | (Ext(vk) ? 1u : 0u);
        SendInput(1, i, Marshal.SizeOf(typeof(INPUT)));
    }
}
'@

$VK = @{ Alt = 0xA4; RAlt = 0xA5; Shift = 0xA0; A = 0x41; C = 0x43; V = 0x56; X = 0x58; Z = 0x5A; Left = 0x25; Right = 0x27; Back = 0x08; End = 0x23 }

$form = New-Object System.Windows.Forms.Form
$form.Text = 'MacShortcuts smoke test'
$form.TopMost = $true
$form.Width = 500; $form.Height = 120
$tb = New-Object System.Windows.Forms.RichTextBox
$tb.Dock = 'Fill'
$form.Controls.Add($tb)
$form.Show()
$form.Activate()
[void][Kb]::SetForegroundWindow($form.Handle)

function Pump([int]$ms = 150) {
    $end = (Get-Date).AddMilliseconds($ms)
    while ((Get-Date) -lt $end) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 10 }
}

function Press([string[]]$mods, [string]$key) {
    if ([Kb]::GetForegroundWindow() -ne $form.Handle) { throw 'Test window lost focus; aborting so keys are not sent elsewhere.' }
    foreach ($m in $mods) { [Kb]::Key($VK[$m], $false); Pump 20 }
    [Kb]::Key($VK[$key], $false); Pump 20
    [Kb]::Key($VK[$key], $true); Pump 20
    [array]::Reverse($mods)
    foreach ($m in $mods) { [Kb]::Key($VK[$m], $true); Pump 20 }
    Pump 150
}

$results = @()
function Check([string]$name, [bool]$ok, [string]$detail) {
    $script:results += [pscustomobject]@{ Test = $name; Result = $(if ($ok) { 'PASS' } else { 'FAIL' }); Detail = $detail }
}

Pump 500
if ([Kb]::GetForegroundWindow() -ne $form.Handle) { Write-Output 'Could not focus the test window; aborting.'; $form.Close(); exit 1 }

try {
    $tb.Text = 'hello world'; $tb.SelectionStart = $tb.Text.Length; $tb.Focus() | Out-Null; Pump
    [System.Windows.Forms.Clipboard]::Clear()

    Press @('Alt') 'A'
    Check 'Alt+A selects all' ($tb.SelectionLength -eq 11) "selection=$($tb.SelectionLength)"

    Press @('Alt') 'C'
    $clip = [System.Windows.Forms.Clipboard]::GetText().TrimEnd()
    Check 'Alt+C copies' ($clip -eq 'hello world') "clipboard='$clip'"

    Press @('Alt') 'Left'
    Check 'Alt+Left -> Home' ($tb.SelectionStart -eq 0 -and $tb.SelectionLength -eq 0) "caret=$($tb.SelectionStart)"

    Press @('Alt') 'Right'
    Check 'Alt+Right -> End' ($tb.SelectionStart -eq 11) "caret=$($tb.SelectionStart)"

    Press @('Alt', 'Shift') 'Left'
    Check 'Alt+Shift+Left selects to line start' ($tb.SelectionStart -eq 0 -and $tb.SelectionLength -eq 11) "start=$($tb.SelectionStart) len=$($tb.SelectionLength)"

    Press @() 'End'
    Press @('Alt') 'Back'
    Check 'Alt+Backspace deletes to line start' ($tb.Text -eq '') "text='$($tb.Text)'"

    Press @('Alt') 'V'
    Check 'Alt+V pastes' ($tb.Text.TrimEnd() -eq 'hello world') "text='$($tb.Text)'"

    # Hold Alt across two shortcuts: select all, then cut.
    [Kb]::Key($VK.Alt, $false); Pump 20
    [Kb]::Key($VK.A, $false); [Kb]::Key($VK.A, $true); Pump 100
    [Kb]::Key($VK.X, $false); [Kb]::Key($VK.X, $true); Pump 100
    [Kb]::Key($VK.Alt, $true); Pump 150
    Check 'Alt held: Alt+A then Alt+X cuts all' ($tb.Text -eq '') "text='$($tb.Text)'"

    # If the menu bar / system menu had been activated by the Alt release, typing would not reach the textbox.
    Press @() 'A'
    Check 'No menu activation after Alt release' ($tb.Text -eq 'a') "text='$($tb.Text)'"

    Press @('RAlt') 'A'
    Press @('RAlt') 'C'
    $clip = [System.Windows.Forms.Clipboard]::GetText().TrimEnd()
    Check 'Right Alt+A, Right Alt+C copies' ($clip -eq 'a') "clipboard='$clip'"
}
catch {
    Write-Output "Aborted: $_"
}
finally {
    # Make sure no modifier is left down.
    foreach ($m in 'Alt', 'RAlt', 'Shift') { [Kb]::Key($VK[$m], $true) }
    $form.Close()
}

$results | Format-Table -AutoSize | Out-String -Width 200
