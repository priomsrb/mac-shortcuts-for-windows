namespace MacShortcuts;

/// <summary>
/// Keeps one instance of the app running per session. Later launches ask the running
/// instance to show its window, then exit.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    const string MutexName = @"Local\MacShortcutsForWindows.Instance";
    const string ShowEventName = @"Local\MacShortcutsForWindows.Show";

    readonly Mutex _mutex;
    readonly EventWaitHandle _showEvent;
    RegisteredWaitHandle? _showWait;

    SingleInstance(Mutex mutex)
    {
        _mutex = mutex;
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
    }

    /// <param name="waitForPrevious">Wait for a running instance to exit (e.g. when restarting as admin).</param>
    /// <returns>null if another instance is running; it has been asked to show its window.</returns>
    public static SingleInstance? TryAcquire(bool waitForPrevious)
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
            return null;
        }

        if (!owned && waitForPrevious)
        {
            try { owned = mutex.WaitOne(TimeSpan.FromSeconds(10)); }
            catch (AbandonedMutexException) { owned = true; }
        }

        if (!owned)
        {
            mutex.Dispose();
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
                using (existing) existing.Set();
            return null;
        }

        return new SingleInstance(mutex);
    }

    /// <summary>Calls <paramref name="onShow"/> on a thread-pool thread whenever a later launch asks to show the window.</summary>
    public void OnShowRequested(Action onShow) =>
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => onShow(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _showWait?.Unregister(null);
        _showEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
