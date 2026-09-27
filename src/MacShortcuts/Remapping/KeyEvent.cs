namespace MacShortcuts.Remapping;

/// <summary>A physical key press or release reported by the keyboard hook.</summary>
internal readonly record struct KeyEvent(int Vk, bool Up, uint ScanCode = 0, bool Extended = false);
