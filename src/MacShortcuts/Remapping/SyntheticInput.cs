namespace MacShortcuts.Remapping;

/// <summary>Input the engine wants injected, in the order given.</summary>
internal abstract record SyntheticInput
{
    /// <summary>A virtual key; the scan code and extended flag are derived from it.</summary>
    public sealed record Key(int Vk, bool Up) : SyntheticInput;

    /// <summary>Replays a physical key event exactly as the keyboard reported it.</summary>
    public sealed record Replay(KeyEvent Original) : SyntheticInput;

    public sealed record LeftButton(bool Up) : SyntheticInput;

    /// <summary>A vertical wheel movement; positive scrolls up (away from the user).</summary>
    public sealed record Wheel(int Delta) : SyntheticInput;
}
