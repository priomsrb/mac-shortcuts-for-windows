using MacShortcuts.Remapping;
using MacShortcuts.Shortcuts;

namespace MacShortcuts.Settings;

/// <summary>
/// The single owner of the app's settings. Every change is saved, then announced through
/// <see cref="Changed"/> so the tray, the settings window and the remapper stay in sync.
/// </summary>
/// <param name="save">Persists the settings; <see cref="AppSettings.Save"/> outside of tests.</param>
internal sealed class SettingsController(AppSettings settings, Action<AppSettings> save)
{
    public event EventHandler? Changed;

    public bool Enabled => settings.Enabled;
    public bool UseLeftAlt => settings.UseLeftAlt;
    public bool UseRightAlt => settings.UseRightAlt;
    public AppTheme Theme => settings.Theme;
    public IReadOnlyList<string> ExcludedApps => settings.ExcludedApps;

    public bool IsEnabled(ShortcutDef shortcut) => settings.IsEnabled(shortcut);

    public RemapConfig ToConfig() => settings.ToConfig();

    public void SetEnabled(bool enabled) => Change(() => settings.Enabled = enabled);

    public void SetUseLeftAlt(bool use) => Change(() => settings.UseLeftAlt = use);

    public void SetUseRightAlt(bool use) => Change(() => settings.UseRightAlt = use);

    public void SetTheme(AppTheme theme) => Change(() => settings.Theme = theme);

    public void SetShortcutEnabled(ShortcutDef shortcut, bool enabled) =>
        Change(() => settings.Shortcuts[shortcut.Id] = enabled);

    public void SetAllShortcutsEnabled(bool enabled) => Change(() =>
    {
        foreach (var shortcut in ShortcutCatalog.All) settings.Shortcuts[shortcut.Id] = enabled;
    });

    public void ResetShortcuts() => Change(settings.Shortcuts.Clear);

    public void SetExcludedApps(IReadOnlyList<string> apps)
    {
        if (apps.SequenceEqual(settings.ExcludedApps)) return;
        Change(() => settings.ExcludedApps = [.. apps]);
    }

    void Change(Action apply)
    {
        apply();
        save(settings);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
