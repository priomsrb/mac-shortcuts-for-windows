using MacShortcuts.Interop;
using MacShortcuts.UI;

namespace MacShortcuts;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var instance = SingleInstance.TryAcquire(waitForPrevious: args.Contains(CommandLineArgs.WaitForPrevious));
        if (instance == null) return;

        ComCtl32.InitCommonControlsEx(new ComCtl32.INITCOMMONCONTROLSEX
        {
            dwSize = 8,
            dwICC = ComCtl32.ICC_STANDARD_CLASSES | ComCtl32.ICC_LISTVIEW_CLASSES | ComCtl32.ICC_TAB_CLASSES,
        });

        using var app = new TrayApp(startMinimized: args.Contains(CommandLineArgs.Minimized));
        instance.OnShowRequested(app.RequestShowSettings);
        app.Run();
    }
}
