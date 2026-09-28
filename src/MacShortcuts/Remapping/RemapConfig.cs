using MacShortcuts.Shortcuts;

namespace MacShortcuts.Remapping;

public sealed record RemapConfig(
    bool Enabled,
    bool LeftAlt,
    bool RightAlt,
    bool CtrlClick,
    bool CtrlScroll,
    IReadOnlyDictionary<Trigger, ShortcutAction> Map,
    IReadOnlySet<string> ExcludedApps)
{
    public static RemapConfig Disabled { get; } = new(false, false, false, false, false,
        new Dictionary<Trigger, ShortcutAction>(), new HashSet<string>());
}
