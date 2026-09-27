using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace MacShortcuts.Platform;

internal static class Elevation
{
    public static bool IsAdmin { get; } =
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <returns>false if the user declined the UAC prompt.</returns>
    public static bool TryRestartElevated()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!, CommandLineArgs.WaitForPrevious)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
