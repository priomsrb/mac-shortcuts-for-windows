namespace MacShortcuts.Remapping;

/// <summary>
/// A key press or release reported by the keyboard hook. <paramref name="Injected"/> is set for input
/// synthesized by other software (e.g. PowerToys Keyboard Manager), as opposed to the physical keyboard.
/// </summary>
internal readonly record struct KeyEvent(int Vk, bool Up, uint ScanCode = 0, bool Extended = false, bool Injected = false);
