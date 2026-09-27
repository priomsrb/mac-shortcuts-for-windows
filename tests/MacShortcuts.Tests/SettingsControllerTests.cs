using MacShortcuts.Settings;
using MacShortcuts.Shortcuts;

namespace MacShortcuts.Tests;

public class SettingsControllerTests
{
    readonly AppSettings _settings = new();
    readonly SettingsController _controller;
    int _saves, _changes;

    public SettingsControllerTests()
    {
        _controller = new SettingsController(_settings, _ => _saves++);
        _controller.Changed += (_, _) =>
        {
            // Listeners must see the settings already saved.
            Assert.Equal(_changes + 1, _saves);
            _changes++;
        };
    }

    [Fact]
    public void Change_SavesThenNotifies()
    {
        _controller.SetEnabled(false);

        Assert.False(_controller.Enabled);
        Assert.False(_controller.ToConfig().Enabled);
        Assert.Equal(1, _saves);
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void SetAllShortcutsEnabled_ThenReset_RestoresDefaults()
    {
        _controller.SetAllShortcutsEnabled(false);
        Assert.All(ShortcutCatalog.All, s => Assert.False(_controller.IsEnabled(s)));

        _controller.ResetShortcuts();
        Assert.All(ShortcutCatalog.All, s => Assert.Equal(s.DefaultEnabled, _controller.IsEnabled(s)));
        Assert.Equal(2, _changes);
    }

    [Fact]
    public void SetExcludedApps_Unchanged_DoesNothing()
    {
        _controller.SetExcludedApps(["mstsc.exe"]);
        _controller.SetExcludedApps(["mstsc.exe"]);

        Assert.Equal(["mstsc.exe"], _controller.ExcludedApps);
        Assert.Equal(1, _saves);
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void SetExcludedApps_CopiesTheList()
    {
        var apps = new List<string> { "mstsc.exe" };
        _controller.SetExcludedApps(apps);
        apps.Add("code.exe");

        Assert.Equal(["mstsc.exe"], _controller.ExcludedApps);
    }
}
