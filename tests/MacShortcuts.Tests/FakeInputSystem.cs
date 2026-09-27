using MacShortcuts.Interop;
using MacShortcuts.Remapping;

namespace MacShortcuts.Tests;

/// <summary>Records injected input and plays the part of the physical keyboard.</summary>
internal sealed class FakeInputSystem : IInputSystem
{
    public HashSet<int> HeldKeys { get; } = [];
    public string ForegroundProcess { get; set; } = "notepad";
    public List<SyntheticInput> Sent { get; } = [];
    public int MinimizeCount { get; private set; }

    public bool IsKeyDown(int vk) => HeldKeys.Contains(vk);
    public string GetForegroundProcessName() => ForegroundProcess;
    public string GetProcessNameAt(POINT point) => ForegroundProcess;
    public void Send(IReadOnlyList<SyntheticInput> inputs) => Sent.AddRange(inputs);
    public void MinimizeForegroundWindow() => MinimizeCount++;
}
