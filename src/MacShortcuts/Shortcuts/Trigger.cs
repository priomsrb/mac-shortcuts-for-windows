namespace MacShortcuts.Shortcuts;

/// <summary>
/// A key pressed while the <paramref name="Via"/> modifier is held, optionally with Shift.
/// <see cref="Mods.Alt"/> is "Cmd"; <see cref="Mods.Ctrl"/> and <see cref="Mods.Win"/> are for the Mac's own Ctrl and for Win-as-Cmd text navigation.
/// </summary>
public readonly record struct Trigger(Keys Key, bool Shift, Mods Via = Mods.Alt);
