using MacShortcuts.UI;

namespace MacShortcuts;

internal static class Program
{
    const string MutexName = @"Local\MacShortcutsForWindows.Instance";
    const string ShowEventName = @"Local\MacShortcutsForWindows.Show";

    [STAThread]
    static void Main(string[] args)
    {
        Mutex mutex;
        bool owned;
        try
        {
            mutex = new Mutex(true, MutexName, out owned);
        }
        catch (UnauthorizedAccessException)
        {
            // Another instance is running as administrator and we can't touch its objects.
            return;
        }

        using (mutex)
        {
            if (!owned && args.Contains(CommandLineArgs.WaitForPrevious))
            {
                // Restarting (e.g. as admin): give the previous instance time to exit.
                try { owned = mutex.WaitOne(TimeSpan.FromSeconds(10)); }
                catch (AbandonedMutexException) { owned = true; }
            }

            if (!owned)
            {
                // Already running: ask that instance to show its window.
                if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
                    using (existing) existing.Set();
                return;
            }

            using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

            ApplicationConfiguration.Initialize();
            var sync = new WindowsFormsSynchronizationContext();
            var app = new TrayApp(startMinimized: args.Contains(CommandLineArgs.Minimized));

            var wait = ThreadPool.RegisterWaitForSingleObject(showEvent,
                (_, _) => sync.Post(_ => app.ShowSettings(), null), null, Timeout.Infinite, executeOnlyOnce: false);

            Application.Run(app);

            wait.Unregister(null);
            mutex.ReleaseMutex();
        }
    }
}
