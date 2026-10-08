using System.Text.Json.Serialization;

namespace MacShortcuts.Shortcuts;

/// <summary>
/// A shortcut the user recorded: a key held with Alt (⌘ Cmd), Ctrl or Win, and optionally Shift,
/// that sends a different key combination instead.
/// </summary>
public sealed class CustomShortcut
{
    public Keys Key { get; set; }

    /// <summary>One or more of <see cref="Mods.Alt"/>, <see cref="Mods.Ctrl"/> and <see cref="Mods.Win"/>.</summary>
    public Mods Via { get; set; } = Mods.Alt;

    public bool Shift { get; set; }
    public Keys SendsKey { get; set; }
    public Mods SendsMods { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>Whether <see cref="Via"/> holds only Alt, Ctrl and Win (at least one), and both keys are real keys.</summary>
    [JsonIgnore]
    public bool IsValid =>
        IsValidVia(Via)
        && Key != 0 && SendsKey != 0
        && !KeyNames.IsModifier((int)Key) && !KeyNames.IsModifier((int)SendsKey);

    [JsonIgnore]
    public Trigger Trigger => new(Key, Shift, Via);

    [JsonIgnore]
    public Chord Sends => new(SendsKey, SendsMods);

    [JsonIgnore]
    public string TriggerText => KeyNames.Format(Via | (Shift ? Mods.Shift : Mods.None), (int)Key);

    [JsonIgnore]
    public string SendsText => KeyNames.Format(SendsMods, (int)SendsKey);

    public static CustomShortcut From(Chord trigger, Chord sends) => new()
    {
        Key = trigger.Key,
        Via = trigger.Mods & ~Mods.Shift,
        Shift = trigger.Mods.HasFlag(Mods.Shift),
        SendsKey = sends.Key,
        SendsMods = sends.Mods,
    };

    /// <summary>Whether a recorded trigger holds at least one of Alt, Ctrl and Win, with or without Shift.</summary>
    public static bool IsValidTrigger(Chord trigger) => IsValidVia(trigger.Mods & ~Mods.Shift);

    static bool IsValidVia(Mods via) => via != Mods.None && (via & ~(Mods.Alt | Mods.Ctrl | Mods.Win)) == Mods.None;
}
