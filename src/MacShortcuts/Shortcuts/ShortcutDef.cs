namespace MacShortcuts.Shortcuts;

public sealed record ShortcutDef(
    string Id,
    string Category,
    string TriggerText,
    string SendsText,
    string Description,
    bool DefaultEnabled,
    IReadOnlyList<Binding> Bindings);
