using MacShortcuts.Remapping;
using MacShortcuts.Settings;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.Tests;

public class CustomShortcutTests
{
    const int K = (int)Keys.K, C = (int)Keys.C, Home = (int)Keys.Home;

    readonly FakeInputSystem _system = new();
    readonly RemapEngine _engine;

    public CustomShortcutTests() => _engine = new RemapEngine(_system);

    static AppSettings With(params CustomShortcut[] shortcuts) => new() { CustomShortcuts = [.. shortcuts] };

    [Fact]
    public void Texts_ListModifiersThenKey()
    {
        var shortcut = CustomShortcut.From(new Chord(Keys.K, Mods.Alt | Mods.Shift), new Chord(Keys.Home, Mods.Ctrl | Mods.Shift));
        Assert.Equal("Alt+Shift+K", shortcut.TriggerText);
        Assert.Equal("Ctrl+Shift+Home", shortcut.SendsText);
    }

    [Theory]
    [InlineData(Mods.Alt, true)]
    [InlineData(Mods.Ctrl | Mods.Shift, true)]
    [InlineData(Mods.Win, true)]
    [InlineData(Mods.None, false)]
    [InlineData(Mods.Shift, false)]
    [InlineData(Mods.Alt | Mods.Ctrl, true)]
    [InlineData(Mods.Shift | Mods.Win | Mods.Ctrl, true)]
    public void IsValidTrigger_NeedsAtLeastOneOfAltCtrlWin(Mods mods, bool valid) =>
        Assert.Equal(valid, CustomShortcut.IsValidTrigger(new Chord(Keys.K, mods)));

    [Fact]
    public void ToConfig_CustomShortcutOverridesCatalog()
    {
        var config = With(CustomShortcut.From(new Chord(Keys.C, Mods.Alt), new Chord(Keys.Home))).ToConfig();
        var action = Assert.IsType<SendKeysAction>(config.Map[new Trigger(Keys.C, false)]);
        Assert.Equal([new Chord(Keys.Home)], action.Chords);
    }

    [Fact]
    public void ToConfig_SkipsDisabledAndInvalidShortcuts()
    {
        var disabled = CustomShortcut.From(new Chord(Keys.J, Mods.Alt), new Chord(Keys.Home));
        disabled.Enabled = false;
        var invalid = new CustomShortcut { Key = Keys.E, Via = Mods.Shift, SendsKey = Keys.Home };

        var map = With(disabled, invalid).ToConfig().Map;

        Assert.DoesNotContain(new Trigger(Keys.J, false), map.Keys);
        Assert.DoesNotContain(new Trigger(Keys.E, false, Mods.Shift), map.Keys);
    }

    [Fact]
    public void AltK_SendsRecordedKeys()
    {
        _engine.Update(With(CustomShortcut.From(new Chord(Keys.K, Mods.Alt), new Chord(Keys.Home, Mods.Ctrl))).ToConfig());
        _engine.OnKey(new KeyEvent(VK_LMENU, Up: false));
        _system.HeldKeys.Add(VK_LMENU);

        Assert.True(_engine.OnKey(new KeyEvent(K, Up: false)));
        Assert.Contains(new SyntheticInput.Key(Home, Up: false), _system.Sent);
        Assert.Contains(new SyntheticInput.Key(VK_LCONTROL, Up: false), _system.Sent);
    }

    [Fact]
    public void CtrlK_SendsRecordedKeysWhileCtrlStaysHeld()
    {
        _engine.Update(With(CustomShortcut.From(new Chord(Keys.K, Mods.Ctrl), new Chord(Keys.Home))).ToConfig());
        _engine.OnKey(new KeyEvent(VK_LCONTROL, Up: false));
        _system.HeldKeys.Add(VK_LCONTROL);

        Assert.True(_engine.OnKey(new KeyEvent(K, Up: false)));
        Assert.Equal(new SyntheticInput.Key(VK_LCONTROL, Up: true), _system.Sent[0]);
        Assert.Equal(new SyntheticInput.Key(VK_LCONTROL, Up: false), _system.Sent[^1]);
    }

    [Fact]
    public void CtrlAltK_SendsRecordedKeys_AndLeavesCtrlHeld()
    {
        _engine.Update(With(CustomShortcut.From(new Chord(Keys.K, Mods.Ctrl | Mods.Alt), new Chord(Keys.Home))).ToConfig());
        Hold(VK_LCONTROL);
        Hold(VK_LMENU);

        Assert.True(_engine.OnKey(new KeyEvent(K, Up: false)));
        Assert.Equal([
            new SyntheticInput.Key(RemapEngine.MaskKey, Up: false), new SyntheticInput.Key(RemapEngine.MaskKey, Up: true),
            new SyntheticInput.Key(VK_LMENU, Up: true),
            new SyntheticInput.Key(VK_LCONTROL, Up: true),
            new SyntheticInput.Key(Home, Up: false), new SyntheticInput.Key(Home, Up: true),
            new SyntheticInput.Key(VK_LCONTROL, Up: false),
        ], _system.Sent);
    }

    [Fact]
    public void CtrlWinK_SendsRecordedKeys_WrappedInMaskKeys()
    {
        _engine.Update(With(CustomShortcut.From(new Chord(Keys.K, Mods.Ctrl | Mods.Win), new Chord(Keys.Home))).ToConfig());
        Hold(VK_LCONTROL);
        Hold(VK_LWIN);

        Assert.True(_engine.OnKey(new KeyEvent(K, Up: false)));
        Assert.Equal(new SyntheticInput.Key(RemapEngine.MaskKey, Up: false), _system.Sent[0]);
        Assert.Equal(new SyntheticInput.Key(RemapEngine.MaskKey, Up: true), _system.Sent[^1]);
    }

    [Fact]
    public void AltWinLeft_SendingWinLeft_KeepsWinHeldAndSendsNoClosingMaskKey()
    {
        // A mask key tap after Win+Left would stop Windows snapping the window.
        _engine.Update(With(CustomShortcut.From(new Chord(Keys.Left, Mods.Alt | Mods.Win), new Chord(Keys.Left, Mods.Win))).ToConfig());
        Hold(VK_LMENU);
        Hold(VK_LWIN);

        Assert.True(_engine.OnKey(new KeyEvent((int)Keys.Left, Up: false)));
        Assert.Equal([
            new SyntheticInput.Key(RemapEngine.MaskKey, Up: false), new SyntheticInput.Key(RemapEngine.MaskKey, Up: true),
            new SyntheticInput.Key(VK_LMENU, Up: true),
            new SyntheticInput.Key(RemapEngine.MaskKey, Up: false), new SyntheticInput.Key(RemapEngine.MaskKey, Up: true),
            new SyntheticInput.Key((int)Keys.Left, Up: false), new SyntheticInput.Key((int)Keys.Left, Up: true),
        ], _system.Sent);
    }

    [Fact]
    public void ExtraModifier_DoesNotTriggerASmallerCombination()
    {
        _engine.Update(With(CustomShortcut.From(new Chord(Keys.K, Mods.Ctrl), new Chord(Keys.Home))).ToConfig());
        Hold(VK_LCONTROL);
        Hold(VK_LWIN);

        Assert.False(_engine.OnKey(new KeyEvent(K, Up: false)));
    }

    void Hold(int vk)
    {
        _system.HeldKeys.Add(vk);
        _engine.OnKey(new KeyEvent(vk, Up: false));
    }

    [Fact]
    public void Capture_ReportsKeyWithHeldModifiers_AndSwallowsIt()
    {
        _engine.Update(new AppSettings().ToConfig());
        var captured = new List<Chord>();
        _engine.SetCapture(captured.Add);

        _engine.OnKey(new KeyEvent(VK_LMENU, Up: false));
        _system.HeldKeys.Add(VK_LMENU);
        _engine.OnKey(new KeyEvent(VK_LSHIFT, Up: false));
        _system.HeldKeys.Add(VK_LSHIFT);

        Assert.True(_engine.OnKey(new KeyEvent(C, Up: false))); // Alt+Shift+C isn't remapped while recording.
        Assert.True(_engine.OnKey(new KeyEvent(C, Up: true)));
        Assert.Equal([new Chord(Keys.C, Mods.Alt | Mods.Shift)], captured);
        Assert.DoesNotContain(new SyntheticInput.Key(VK_LCONTROL, Up: false), _system.Sent);
        Assert.Contains(new SyntheticInput.Key(RemapEngine.MaskKey, Up: false), _system.Sent); // Releasing Alt won't open the menu bar.
    }

    [Fact]
    public void Capture_Stopped_RemapsAgain()
    {
        _engine.Update(new AppSettings().ToConfig());
        _engine.SetCapture(_ => { });
        _engine.SetCapture(null);

        _engine.OnKey(new KeyEvent(VK_LMENU, Up: false));
        _system.HeldKeys.Add(VK_LMENU);
        Assert.True(_engine.OnKey(new KeyEvent(C, Up: false)));
        Assert.Contains(new SyntheticInput.Key(VK_LCONTROL, Up: false), _system.Sent);
    }

    [Fact]
    public void Controller_AddsRejectsDuplicatesTogglesAndRemoves()
    {
        var controller = new SettingsController(new AppSettings(), _ => { });
        var shortcut = CustomShortcut.From(new Chord(Keys.K, Mods.Alt), new Chord(Keys.Home));

        Assert.True(controller.AddCustomShortcut(shortcut));
        Assert.False(controller.AddCustomShortcut(CustomShortcut.From(new Chord(Keys.K, Mods.Alt), new Chord(Keys.End))));
        controller.SetCustomShortcutEnabled(0, false);
        Assert.False(controller.CustomShortcuts[0].Enabled);
        controller.RemoveCustomShortcut(0);
        Assert.Empty(controller.CustomShortcuts);
    }
}
