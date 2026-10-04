using MacShortcuts.Remapping;
using MacShortcuts.Settings;
using static MacShortcuts.Interop.User32;

namespace MacShortcuts.Tests;

public class RemapEngineTests
{
    const int A = (int)Keys.A, C = (int)Keys.C, J = (int)Keys.J, M = (int)Keys.M, Q = (int)Keys.Q;
    const int X = (int)Keys.X, Y = (int)Keys.Y, Z = (int)Keys.Z;
    const int Tab = (int)Keys.Tab, Left = (int)Keys.Left, Home = (int)Keys.Home, F4 = (int)Keys.F4;
    const int Mask = RemapEngine.MaskKey;

    readonly FakeInputSystem _system = new();
    readonly RemapEngine _engine;

    public RemapEngineTests()
    {
        _engine = new RemapEngine(_system);
        Configure(new AppSettings());
    }

    [Fact]
    public void AltC_SendsCtrlC()
    {
        Assert.False(Press(VK_LMENU));
        Assert.True(Press(C));
        AssertSent([.. ReleasedAlt(VK_LMENU), KeyDown(VK_LCONTROL), KeyDown(C), KeyUp(C), KeyUp(VK_LCONTROL)]);

        Assert.True(Release(C));
        Assert.True(Release(VK_LMENU)); // Already released logically.
        AssertSent([]);
    }

    [Fact]
    public void UnmappedAltCombo_PassesThrough()
    {
        Assert.False(Press(VK_LMENU));
        Assert.False(Press(Tab));
        Assert.False(Release(Tab));
        Assert.False(Release(VK_LMENU));
        AssertSent([]);
    }

    [Fact]
    public void AltHeldAfterShortcut_ReinjectsAltForUnmappedKey()
    {
        Press(VK_LMENU);
        Tap(C);
        _system.Sent.Clear();

        Assert.True(Press(Tab));
        AssertSent([KeyDown(VK_LMENU), new SyntheticInput.Replay(new KeyEvent(Tab, Up: false))]);
        Assert.False(Release(Tab));
        Assert.False(Release(VK_LMENU)); // Windows thinks Alt is down again, so it needs the key-up.
    }

    [Fact]
    public void AltHeld_TwoShortcutsInARow()
    {
        Press(VK_LMENU);
        Tap(A);
        _system.Sent.Clear();

        Assert.True(Press(X));
        AssertSent([KeyDown(VK_LCONTROL), KeyDown(X), KeyUp(X), KeyUp(VK_LCONTROL)]);
    }

    [Fact]
    public void AltAutoRepeat_AfterShortcut_KeepsAltReleased()
    {
        Press(VK_LMENU);
        Tap(C);
        Assert.True(Press(VK_LMENU));
    }

    [Fact]
    public void HeldShift_IsKeptForChordsThatUseIt()
    {
        Press(VK_LMENU);
        Press(VK_LSHIFT);
        Assert.True(Press(Left));
        AssertSent([.. ReleasedAlt(VK_LMENU), KeyDown(Home), KeyUp(Home)]);
    }

    [Fact]
    public void HeldShift_IsLiftedForChordsWithoutIt_ThenRestored()
    {
        Press(VK_LMENU);
        Press(VK_LSHIFT);
        Assert.True(Press(Z)); // Alt+Shift+Z -> Ctrl+Y
        AssertSent([.. ReleasedAlt(VK_LMENU),
            KeyDown(VK_LCONTROL), KeyUp(VK_LSHIFT), KeyDown(Y), KeyUp(Y),
            KeyUp(VK_LCONTROL), KeyDown(VK_LSHIFT)]);
    }

    [Fact]
    public void ChordWithAlt_PressesLeftAlt()
    {
        Press(VK_LMENU);
        Assert.True(Press(Q)); // Alt+Q -> Alt+F4
        AssertSent([.. ReleasedAlt(VK_LMENU), KeyDown(VK_LMENU), KeyDown(F4), KeyUp(F4), KeyUp(VK_LMENU)]);
    }

    [Fact]
    public void AltM_MinimizesWindow()
    {
        Press(VK_LMENU);
        Assert.True(Press(M));
        AssertSent([.. ReleasedAlt(VK_LMENU)]);
        Assert.Equal(1, _system.MinimizeCount);
    }

    [Theory]
    [InlineData(VK_LCONTROL)]
    [InlineData(VK_RWIN)]
    public void WithCtrlOrWinHeld_NothingIsRemapped(int modifier)
    {
        Press(modifier);
        Press(VK_LMENU);
        Assert.False(Press(C));
        AssertSent([]);
    }

    [Fact]
    public void WhenDisabled_PassesThrough()
    {
        Configure(new AppSettings { Enabled = false });
        Press(VK_LMENU);
        Assert.False(Press(C));
        AssertSent([]);
    }

    [Fact]
    public void LeftAltOff_OnlyRightAltActsAsCmd()
    {
        Configure(new AppSettings { UseLeftAlt = false });

        Press(VK_LMENU);
        Assert.False(Press(C));
        Release(C);
        Release(VK_LMENU);
        AssertSent([]);

        Press(VK_RMENU);
        Assert.True(Press(C));
        AssertSent([.. ReleasedAlt(VK_RMENU), KeyDown(VK_LCONTROL), KeyDown(C), KeyUp(C), KeyUp(VK_LCONTROL)]);
    }

    [Fact]
    public void AltGrFakeCtrl_DoesNotCountAsCtrl()
    {
        Press(new KeyEvent(VK_LCONTROL, Up: false, ScanCode: RemapEngine.AltGrFakeCtrlScanCode));
        Press(VK_RMENU);
        Assert.True(Press(C));
    }

    [Fact]
    public void ExcludedApp_PassesThrough()
    {
        Configure(new AppSettings { ExcludedApps = ["Notepad.exe"] });
        Press(VK_LMENU);

        _system.ForegroundProcess = "notepad";
        Assert.False(Press(C));
        Release(C);

        _system.ForegroundProcess = "code";
        Assert.True(Press(C));
    }

    [Fact]
    public void MissedModifierKeyUp_IsReconciled()
    {
        Press(VK_LSHIFT);
        _system.HeldKeys.Remove(VK_LSHIFT); // Released on the lock screen, so the hook never saw it.

        Press(VK_LMENU);
        Assert.True(Press(C)); // Alt+C, not Alt+Shift+C.
        AssertSent([.. ReleasedAlt(VK_LMENU), KeyDown(VK_LCONTROL), KeyDown(C), KeyUp(C), KeyUp(VK_LCONTROL)]);
    }

    [Fact]
    public void CatchAllLetters_OffByDefault()
    {
        Press(VK_LMENU);
        Assert.False(Press(J));
    }

    [Fact]
    public void CatchAllLetters_WhenOn_SendCtrlLetter()
    {
        var settings = new AppSettings();
        settings.Shortcuts["other.letters"] = true;
        Configure(settings);

        Press(VK_LMENU);
        Assert.True(Press(J));
        AssertSent([.. ReleasedAlt(VK_LMENU), KeyDown(VK_LCONTROL), KeyDown(J), KeyUp(J), KeyUp(VK_LCONTROL)]);
    }

    [Fact]
    public void AltClick_SendsCtrlClick()
    {
        Press(VK_LMENU);
        Assert.True(_engine.OnLeftButton(down: true, default));
        AssertSent([.. ReleasedAlt(VK_LMENU), KeyDown(VK_LCONTROL), new SyntheticInput.LeftButton(Up: false)]);

        Assert.True(_engine.OnLeftButton(down: false, default));
        AssertSent([new SyntheticInput.LeftButton(Up: true), KeyUp(VK_LCONTROL)]);
    }

    [Fact]
    public void AltClick_InAltTabSwitcher_PassesThrough()
    {
        Press(VK_LMENU);
        Tap(Tab);
        Assert.False(_engine.OnLeftButton(down: true, default));
        Assert.False(_engine.OnLeftButton(down: false, default));
        Assert.False(_engine.OnWheel(120, default));
        AssertSent([]);
    }

    [Fact]
    public void AltClick_AfterAltTabEnds_SendsCtrlClickAgain()
    {
        Press(VK_LMENU);
        Tap(Tab);
        Release(VK_LMENU);

        Press(VK_LMENU);
        Assert.True(_engine.OnLeftButton(down: true, default));
        AssertSent([.. ReleasedAlt(VK_LMENU), KeyDown(VK_LCONTROL), new SyntheticInput.LeftButton(Up: false)]);
    }

    [Fact]
    public void AltClick_InAltTabSwitcherAfterShortcut_PassesThrough()
    {
        Press(VK_LMENU);
        Tap(C);
        Tap(Tab); // Re-presses Alt, opening the switcher.
        _system.Sent.Clear();

        Assert.False(_engine.OnLeftButton(down: true, default));
        AssertSent([]);
    }

    [Fact]
    public void PlainClick_PassesThrough()
    {
        Assert.False(_engine.OnLeftButton(down: true, default));
        Assert.False(_engine.OnLeftButton(down: false, default));
        AssertSent([]);
    }

    [Fact]
    public void AltScroll_SendsCtrlScroll()
    {
        Press(VK_LMENU);
        Assert.True(_engine.OnWheel(-120, default));
        AssertSent([.. ReleasedAlt(VK_LMENU), KeyDown(VK_LCONTROL), new SyntheticInput.Wheel(-120), KeyUp(VK_LCONTROL)]);

        // Alt is already released logically, so later notches only wrap the wheel in Ctrl.
        Assert.True(_engine.OnWheel(120, default));
        AssertSent([KeyDown(VK_LCONTROL), new SyntheticInput.Wheel(120), KeyUp(VK_LCONTROL)]);
    }

    [Fact]
    public void AltScroll_DuringAltClick_KeepsCtrlHeld()
    {
        Press(VK_LMENU);
        _engine.OnLeftButton(down: true, default);
        AssertSent([.. ReleasedAlt(VK_LMENU), KeyDown(VK_LCONTROL), new SyntheticInput.LeftButton(Up: false)]);

        Assert.True(_engine.OnWheel(120, default));
        AssertSent([new SyntheticInput.Wheel(120)]);
    }

    [Fact]
    public void PlainScroll_PassesThrough()
    {
        Assert.False(_engine.OnWheel(120, default));
        AssertSent([]);
    }

    [Fact]
    public void AltScroll_PassesThroughWhenDisabled()
    {
        var settings = new AppSettings();
        settings.Shortcuts[ShortcutCatalog.CtrlScroll.Id] = false;
        Configure(settings);

        Press(VK_LMENU);
        Assert.False(_engine.OnWheel(120, default));
        AssertSent([]);
    }

    [Fact]
    public void CtrlA_PassesThroughByDefault()
    {
        Press(VK_LCONTROL);
        Assert.False(Press(A));
        AssertSent([]);
    }

    [Fact]
    public void CtrlA_WhenOn_SendsHomeAndKeepsCtrlHeld()
    {
        Enable("nav.emacsLineEnds");
        Press(VK_LCONTROL);
        Assert.True(Press(A));
        AssertSent([KeyUp(VK_LCONTROL), KeyDown(Home), KeyUp(Home), KeyDown(VK_LCONTROL)]);
        Assert.True(Release(A));
    }

    [Fact]
    public void WinLeft_WhenOn_SendsCtrlLeftAndMasksStartMenu()
    {
        Enable("nav.winWord");
        Press(VK_LWIN);
        Assert.True(Press(Left));
        AssertSent([KeyDown(Mask), KeyUp(Mask), KeyDown(VK_LCONTROL), KeyUp(VK_LWIN), KeyDown(Left), KeyUp(Left),
            KeyUp(VK_LCONTROL), KeyDown(VK_LWIN), KeyDown(Mask), KeyUp(Mask)]);
    }

    [Fact]
    public void WinShiftLeft_WhenOn_SelectsWord()
    {
        Enable("nav.winSelectWord");
        Press(VK_LWIN);
        Press(VK_LSHIFT);
        Assert.True(Press(Left));
        AssertSent([KeyDown(Mask), KeyUp(Mask), KeyDown(VK_LCONTROL), KeyUp(VK_LWIN), KeyDown(Left), KeyUp(Left),
            KeyUp(VK_LCONTROL), KeyDown(VK_LWIN), KeyDown(Mask), KeyUp(Mask)]);
    }

    [Fact]
    public void AltDelete_SendsCtrlDelete()
    {
        Press(VK_LMENU);
        Assert.True(Press((int)Keys.Delete));
    }

    void Enable(string id)
    {
        var settings = new AppSettings();
        settings.Shortcuts[id] = true;
        Configure(settings);
    }

    void Configure(AppSettings settings) => _engine.Update(settings.ToConfig());

    bool Press(int vk) => Press(new KeyEvent(vk, Up: false));

    bool Press(KeyEvent key)
    {
        _system.HeldKeys.Add(key.Vk);
        return _engine.OnKey(key);
    }

    bool Release(int vk)
    {
        _system.HeldKeys.Remove(vk);
        return _engine.OnKey(new KeyEvent(vk, Up: true));
    }

    void Tap(int vk)
    {
        Press(vk);
        Release(vk);
    }

    static SyntheticInput.Key KeyDown(int vk) => new SyntheticInput.Key(vk, Up: false);
    static SyntheticInput.Key KeyUp(int vk) => new SyntheticInput.Key(vk, Up: true);

    /// <summary>Tapping the mask key, then releasing Alt so Windows sees it lifted.</summary>
    static SyntheticInput[] ReleasedAlt(int altVk) => [KeyDown(Mask), KeyUp(Mask), KeyUp(altVk)];

    /// <summary>Asserts what was injected since the last call.</summary>
    void AssertSent(SyntheticInput[] expected)
    {
        Assert.Equal(expected, _system.Sent);
        _system.Sent.Clear();
    }
}
