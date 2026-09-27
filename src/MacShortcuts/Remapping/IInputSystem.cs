using MacShortcuts.Interop;

namespace MacShortcuts.Remapping;

/// <summary>The parts of Windows <see cref="RemapEngine"/> depends on, so it can be tested without a real keyboard.</summary>
internal interface IInputSystem
{
    /// <summary>Whether the key is physically down right now.</summary>
    bool IsKeyDown(int vk);

    /// <summary>Lower-case process name without ".exe" (e.g. "mstsc"), or "" if unknown.</summary>
    string GetForegroundProcessName();

    /// <inheritdoc cref="GetForegroundProcessName"/>
    string GetProcessNameAt(POINT point);

    void Send(IReadOnlyList<SyntheticInput> inputs);

    void MinimizeForegroundWindow();
}
