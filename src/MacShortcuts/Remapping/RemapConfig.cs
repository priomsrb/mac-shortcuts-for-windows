using MacShortcuts.Shortcuts;

namespace MacShortcuts.Remapping;

public sealed record RemapConfig(
    bool Enabled,
    bool LeftAlt,
    bool RightAlt,
    bool CtrlClick,
    IReadOnlyDictionary<Trigger, ShortcutAction> Map,
    IReadOnlySet<string> ExcludedApps);
