using static MacShortcuts.Interop.Shell32;

namespace MacShortcuts.UI;

/// <summary>
/// The notification-area icon. Clicks arrive at <paramref name="owner"/> as <paramref name="callbackMessage"/>,
/// with the event (e.g. <see cref="NIN_SELECT"/>) in the low word of lParam.
/// </summary>
internal sealed unsafe class TrayIcon(IntPtr owner, int callbackMessage)
{
    const uint Id = 1;

    IntPtr _icon;
    string _tip = "";
    bool _added;

    public void Update(IntPtr icon, string tip)
    {
        _icon = icon;
        _tip = tip;
        var data = Data(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        if (_added)
        {
            Shell_NotifyIcon(NIM_MODIFY, data);
            return;
        }
        // Fails if the taskbar isn't up yet; Recreate() tries again once it is.
        _added = Shell_NotifyIcon(NIM_ADD, data);
        if (_added)
        {
            data.uVersion = NOTIFYICON_VERSION_4;
            Shell_NotifyIcon(NIM_SETVERSION, data);
        }
    }

    /// <summary>Adds the icon again after Explorer restarted (the "TaskbarCreated" message).</summary>
    public void Recreate()
    {
        _added = false;
        Update(_icon, _tip);
    }

    public void ShowBalloon(string title, string text, bool warning)
    {
        var data = Data(NIF_INFO);
        Copy(title, data.szInfoTitle, 64);
        Copy(text, data.szInfo, 256);
        data.dwInfoFlags = warning ? NIIF_WARNING : NIIF_INFO;
        Shell_NotifyIcon(NIM_MODIFY, data);
    }

    public void Remove()
    {
        if (_added) Shell_NotifyIcon(NIM_DELETE, Data(0));
        _added = false;
    }

    NOTIFYICONDATAW Data(uint flags)
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = owner,
            uID = Id,
            uFlags = flags,
            uCallbackMessage = (uint)callbackMessage,
            hIcon = _icon,
        };
        Copy(_tip, data.szTip, 128);
        return data;
    }
}
