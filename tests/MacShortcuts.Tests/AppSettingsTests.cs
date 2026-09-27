using MacShortcuts.Settings;
using MacShortcuts.Shortcuts;

namespace MacShortcuts.Tests;

public class AppSettingsTests
{
    [Fact]
    public void ToConfig_NormalisesExcludedApps()
    {
        var settings = new AppSettings { ExcludedApps = [" Notepad.EXE ", "", "mstsc", "  "] };
        Assert.Equal(["mstsc", "notepad"], settings.ToConfig().ExcludedApps.Order());
    }

    [Fact]
    public void IsEnabled_UsesDefaultUnlessOverridden()
    {
        var copy = ShortcutCatalog.All.Single(s => s.Id == "edit.copy");
        var settings = new AppSettings();
        Assert.Equal(copy.DefaultEnabled, settings.IsEnabled(copy));

        settings.Shortcuts[copy.Id] = !copy.DefaultEnabled;
        Assert.Equal(!copy.DefaultEnabled, settings.IsEnabled(copy));
    }

    [Fact]
    public void ToConfig_OnlyMapsEnabledShortcuts()
    {
        var settings = new AppSettings();
        settings.Shortcuts["edit.copy"] = false;
        var map = settings.ToConfig().Map;

        Assert.False(map.ContainsKey(new Trigger(Keys.C, Shift: false)));
        Assert.True(map.ContainsKey(new Trigger(Keys.V, Shift: false)));
    }

    [Fact]
    public void ToConfig_CtrlClickFollowsItsShortcut()
    {
        var settings = new AppSettings();
        Assert.True(settings.ToConfig().CtrlClick);

        settings.Shortcuts[ShortcutCatalog.MouseCtrlClickId] = false;
        Assert.False(settings.ToConfig().CtrlClick);
    }
}
