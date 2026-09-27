namespace MacShortcuts.Shortcuts;

/// <summary>A key pressed while "Cmd" (Alt) is held, optionally with Shift.</summary>
public readonly record struct Trigger(Keys Key, bool Shift);
