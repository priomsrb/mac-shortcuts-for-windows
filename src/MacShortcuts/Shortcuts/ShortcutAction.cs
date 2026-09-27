namespace MacShortcuts.Shortcuts;

public abstract record ShortcutAction;
public sealed record SendKeysAction(IReadOnlyList<Chord> Chords) : ShortcutAction;
public sealed record MinimizeWindowAction : ShortcutAction;
