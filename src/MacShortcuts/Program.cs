using MacShortcuts.UI;

namespace MacShortcuts;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var instance = SingleInstance.TryAcquire(waitForPrevious: args.Contains(CommandLineArgs.WaitForPrevious));
        if (instance == null) return;

        ApplicationConfiguration.Initialize();
        var sync = new WindowsFormsSynchronizationContext();
        var app = new TrayApp(startMinimized: args.Contains(CommandLineArgs.Minimized));
        instance.OnShowRequested(() => sync.Post(_ => app.ShowSettings(), null));

        Application.Run(app);
    }
}
