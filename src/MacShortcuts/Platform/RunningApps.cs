using System.ComponentModel;
using System.Diagnostics;
using MacShortcuts.Interop;

namespace MacShortcuts.Platform;

/// <param name="ProcessName">The executable's file name, e.g. "mstsc.exe".</param>
/// <param name="Description">The executable's description, e.g. "Remote Desktop Connection", or its name without ".exe".</param>
internal sealed record RunningApp(string ProcessName, string Description);

/// <summary>Apps that have a window open, for picking which to exclude.</summary>
internal static class RunningApps
{
    /// <summary>One entry per executable, sorted by description.</summary>
    public static IReadOnlyList<RunningApp> Find()
    {
        var apps = new Dictionary<string, RunningApp>(StringComparer.OrdinalIgnoreCase);
        int self = Environment.ProcessId;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == self || process.MainWindowHandle == IntPtr.Zero || IsCloaked(process.MainWindowHandle)) continue;
                    string name = process.ProcessName + ".exe";
                    if (apps.ContainsKey(name)) continue;
                    apps[name] = new RunningApp(name, Describe(process) ?? process.ProcessName);
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    // The process exited, or it's elevated or protected.
                }
            }
        }
        return [.. apps.Values.OrderBy(a => a.Description, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>
    /// Whether the window manager hides the window, like those of background hosts such as
    /// TextInputHost.exe, even though it counts as visible.
    /// </summary>
    static bool IsCloaked(IntPtr window) =>
        DwmApi.DwmGetWindowAttribute(window, DwmApi.DWMWA_CLOAKED, out int cloaked, sizeof(int)) >= 0 && cloaked != 0;

    static string? Describe(Process process)
    {
        try
        {
            return process.MainModule?.FileName is { } path ? Describe(path) : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Elevated processes can't be inspected without admin rights.
            return null;
        }
    }

    /// <summary>The description of an app that comes with Windows (e.g. "mstsc.exe"), or null.</summary>
    public static string? DescribeWindowsApp(string processName)
    {
        foreach (string folder in (ReadOnlySpan<string>)[Environment.SystemDirectory, Environment.GetFolderPath(Environment.SpecialFolder.Windows)])
        {
            string path = Path.Combine(folder, processName);
            if (File.Exists(path)) return Describe(path);
        }
        return null;
    }

    /// <summary>The description in an executable's version information, or null.</summary>
    public static string? Describe(string path)
    {
        try
        {
            string? description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
            return string.IsNullOrEmpty(description) ? null : description;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
