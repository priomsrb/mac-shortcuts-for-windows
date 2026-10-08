using System.Diagnostics;
using MacShortcuts.Interop;
using MacShortcuts.Platform;
using MacShortcuts.Settings;
using MacShortcuts.UI.Drawing;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.UI;

// The Settings page: a single column of grouped options.
internal sealed unsafe partial class MainWindow
{
    const string StorageText = @"Settings are saved to %APPDATA%\MacShortcuts\settings.json";

    /// <param name="Control">A checkbox (whose text is the title), a control shown to the right, or zero for none.</param>
    readonly record struct Setting(string Title, string Description, IntPtr Control, bool IsCheckBox);

    IntPtr _enabledBox;
    IntPtr _startupBox;
    IntPtr _leftAltBox;
    IntPtr _rightAltBox;
    IntPtr _ignoreInjectedBox;
    IntPtr _theme;
    IntPtr _admin;
    IntPtr _openFolder;

    void CreateSettingsPage()
    {
        _enabledBox = Button("Enable remapping", ButtonKind.CheckBox, Backdrop.Surface, Page.Settings);
        _startupBox = Button("Start with Windows", ButtonKind.CheckBox, Backdrop.Surface, Page.Settings);
        _leftAltBox = Button("Left Alt acts as ⌘ Cmd", ButtonKind.CheckBox, Backdrop.Surface, Page.Settings);
        _rightAltBox = Button("Right Alt acts as ⌘ Cmd", ButtonKind.CheckBox, Backdrop.Surface, Page.Settings);
        _ignoreInjectedBox = Button("Ignore keys from other apps", ButtonKind.CheckBox, Backdrop.Surface, Page.Settings);
        _theme = Child(Page.Settings, "COMBOBOX", null, CBS_DROPDOWNLIST | WS_VSCROLL | WS_TABSTOP);
        foreach (string name in Enum.GetNames<AppTheme>()) SendMessage(_theme, CB_ADDSTRING, IntPtr.Zero, name);
        if (!Elevation.IsAdmin) _admin = Button("Restart as administrator", ButtonKind.Normal, Backdrop.Surface, Page.Settings);
        _openFolder = Button("Open folder", ButtonKind.Link, Backdrop.Window, Page.Settings);
    }

    (string Header, Setting[] Settings)[] SettingSections() =>
    [
        ("General",
        [
            new("Enable remapping", "Turns every shortcut on or off at once. Also in the tray icon's menu.", _enabledBox, true),
            new("Start with Windows", "Starts in the tray when you sign in. Always starts without admin rights.", _startupBox, true),
        ]),
        ("⌘ Cmd key",
        [
            new("Left Alt acts as ⌘ Cmd", "The key where ⌘ sits on a Mac keyboard.", _leftAltBox, true),
            new("Right Alt acts as ⌘ Cmd", "On layouts with AltGr (German, French…), this replaces AltGr characters such as €.", _rightAltBox, true),
        ]),
        ("Compatibility",
        [
            new("Ignore keys from other apps", "Don't remap keys that other software sends, such as PowerToys. Only your physical keyboard triggers shortcuts.", _ignoreInjectedBox, true),
        ]),
        ("Appearance",
        [
            new("Theme", "System follows Windows' light or dark app mode.", _theme, false),
        ]),
        ("Administrator",
        [
            Elevation.IsAdmin
                ? new("Running as administrator", "Shortcuts also work in elevated apps like Task Manager and admin terminals.", IntPtr.Zero, false)
                : new("Not running as administrator", "Windows blocks shortcuts in elevated apps like Task Manager or admin terminals.", _admin, false),
        ]),
    ];

    bool OnSettingsCommand(IntPtr control, int code)
    {
        if (control == _theme)
        {
            // Deferred: applying a theme recreates this window.
            if (code == CBN_SELCHANGE)
                PostMessage(Handle, WM_SET_THEME, SendMessage(_theme, CB_GETCURSEL, IntPtr.Zero, IntPtr.Zero), IntPtr.Zero);
            return true;
        }
        if (code != BN_CLICKED) return false;

        if (control == _enabledBox) _settings.SetEnabled(IsChecked(_enabledBox));
        else if (control == _startupBox) SetStartup(IsChecked(_startupBox));
        else if (control == _leftAltBox) _settings.SetUseLeftAlt(IsChecked(_leftAltBox));
        else if (control == _rightAltBox) _settings.SetUseRightAlt(IsChecked(_rightAltBox));
        else if (control == _ignoreInjectedBox) _settings.SetIgnoreInjectedKeys(IsChecked(_ignoreInjectedBox));
        else if (control == _admin && _admin != IntPtr.Zero) RestartAsAdminRequested?.Invoke(this, EventArgs.Empty);
        else if (control == _openFolder) OpenSettingsFolder();
        else return false;
        return true;
    }

    void LoadSettingsPage()
    {
        SetChecked(_startupBox, StartWithWindows.IsEnabled);
        SendMessage(_theme, CB_SETCURSEL, (int)_settings.Theme, IntPtr.Zero);
    }

    void SyncSettingsPage()
    {
        SetChecked(_enabledBox, _settings.Enabled);
        SetChecked(_leftAltBox, _settings.UseLeftAlt);
        SetChecked(_rightAltBox, _settings.UseRightAlt);
        SetChecked(_ignoreInjectedBox, _settings.IgnoreInjectedKeys);
        foreach (var box in (ReadOnlySpan<IntPtr>)[_enabledBox, _startupBox, _leftAltBox, _rightAltBox, _ignoreInjectedBox])
            InvalidateRect(box, IntPtr.Zero, false);
    }

    void SetStartup(bool enabled)
    {
        try
        {
            StartWithWindows.Set(enabled);
        }
        catch (Exception ex)
        {
            MessageBox(Handle, $"Couldn't update the startup setting:\n{ex.Message}", Title, MB_OK | MB_ICONWARNING);
            _loading = true;
            SetChecked(_startupBox, StartWithWindows.IsEnabled);
            _loading = false;
        }
        InvalidateRect(_startupBox, IntPtr.Zero, false);
    }

    static void OpenSettingsFolder()
    {
        Directory.CreateDirectory(AppSettings.Folder);
        Process.Start(new ProcessStartInfo(AppSettings.Folder) { UseShellExecute = true })?.Dispose();
    }

    void LayoutSettingsPage(RECT client)
    {
        int width = Math.Min(Scale(680), client.right - RailWidth - 2 * Scale(28));
        int left = RailWidth + (client.right - RailWidth - width) / 2, right = left + width;
        int y = Scale(24);

        var title = Paint.Rect(left, y, width, Scale(32));
        _art.Add(hdc => Paint.Text(hdc, "Settings", _fonts.Title, _palette.Text, title));
        y = title.bottom + Scale(18);

        int pad = Scale(14), box = Scale(16), boxGap = Scale(12);
        int captionHeight = Paint.Measure("A", _fonts.CaptionStrong).cy;
        int titleHeight = Paint.Measure("Ag", _fonts.Label).cy;
        foreach (var (header, settings) in SettingSections())
        {
            var headerRect = Paint.Rect(left + Scale(2), y, width, captionHeight);
            string caption = header.ToUpperInvariant();
            _art.Add(hdc =>
            {
                // Letter-spaced, like small caps.
                _ = Gdi32.SetTextCharacterExtra(hdc, Scale(1));
                Paint.Text(hdc, caption, _fonts.CaptionStrong, _palette.MutedText, headerRect);
                _ = Gdi32.SetTextCharacterExtra(hdc, 0);
            });
            y = headerRect.bottom + Scale(8);

            // The panel is painted first, then each row on it.
            int panelTop = y;
            var rows = new List<Action<IntPtr>>();
            for (int i = 0; i < settings.Length; i++)
            {
                var setting = settings[i];
                int rowTop = y;
                int textLeft = left + Scale(16) + (setting.IsCheckBox ? box + boxGap : 0);
                int controlWidth = setting.IsCheckBox || setting.Control == IntPtr.Zero ? 0
                    : setting.Control == _theme ? Scale(140) : ButtonWidth(setting.Control);
                int textRight = right - Scale(16) - (controlWidth > 0 ? controlWidth + Scale(16) : 0);
                int descriptionHeight = Paint.MeasureWrapped(setting.Description, _fonts.Small, textRight - textLeft);
                int rowHeight = pad + titleHeight + Scale(3) + descriptionHeight + pad;

                var titleRect = Paint.Rect(textLeft, rowTop + pad, textRight - textLeft, titleHeight);
                var descriptionRect = Paint.Rect(textLeft, titleRect.bottom + Scale(3), textRight - textLeft, descriptionHeight);
                if (setting.IsCheckBox)
                {
                    // The checkbox draws its own title.
                    int labelWidth = box + boxGap + Paint.Measure(setting.Title, _fonts.Label).cx + Scale(4);
                    Place(setting.Control, Paint.Rect(left + Scale(16), rowTop + pad, labelWidth, titleHeight));
                }
                else
                {
                    rows.Add(hdc => Paint.Text(hdc, setting.Title, _fonts.Label, _palette.Text, titleRect));
                    if (setting.Control == _theme)
                    {
                        // A closed drop-down list sizes itself to its font; its window also holds the list.
                        GetClientRect(_theme, out var field);
                        Place(_theme, Paint.Rect(right - Scale(16) - controlWidth, rowTop + (rowHeight - field.bottom) / 2, controlWidth, Scale(200)));
                    }
                    else if (setting.Control != IntPtr.Zero)
                    {
                        Place(setting.Control, Paint.Rect(right - Scale(16) - controlWidth, rowTop + (rowHeight - Scale(32)) / 2, controlWidth, Scale(32)));
                    }
                }
                rows.Add(hdc => Paint.Text(hdc, setting.Description, _fonts.Small, _palette.MutedText, descriptionRect, DT_WORDBREAK));

                y += rowHeight;
                int dividerY = y;
                if (i < settings.Length - 1) rows.Add(hdc => Paint.HorizontalLine(hdc, left + 1, right - 1, dividerY, _palette.Divider));
            }
            var panel = Paint.Rect(left, panelTop, width, y - panelTop);
            _art.Add(hdc => Paint.Panel(hdc, panel, Scale(8), _palette.Surface, _palette.Border, _palette.Window));
            _art.AddRange(rows);
            y = panel.bottom + Scale(20);
        }

        // Where the settings are stored.
        int iconSize = Scale(14), lineHeight = Scale(22);
        var icon = Paint.Rect(left + Scale(2), y + (lineHeight - iconSize) / 2, iconSize, iconSize);
        int textWidth = Paint.Measure(StorageText, _fonts.Small).cx;
        var storage = Paint.Rect(icon.right + Scale(8), y, textWidth, lineHeight);
        Place(_openFolder, Paint.Rect(storage.right + Scale(8), y, Paint.Measure(GetText(_openFolder), _fonts.Small).cx + Scale(2), lineHeight));
        _art.Add(hdc =>
        {
            Canvas.Render(hdc, icon, _palette.Window, c => Glyphs.Draw(c, Icon.Folder, 0, 0, iconSize, _palette.MutedText));
            Paint.Text(hdc, StorageText, _fonts.Small, _palette.MutedText, storage);
        });
    }
}
