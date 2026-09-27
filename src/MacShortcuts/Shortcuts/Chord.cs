namespace MacShortcuts.Shortcuts;

/// <summary>A key pressed together with a set of modifiers.</summary>
public readonly record struct Chord(Keys Key, Mods Mods = Mods.None);
