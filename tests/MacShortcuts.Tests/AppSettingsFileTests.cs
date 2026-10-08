using MacShortcuts.Settings;
using MacShortcuts.Shortcuts;

namespace MacShortcuts.Tests;

public sealed class AppSettingsFileTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "MacShortcuts.Tests", Guid.NewGuid().ToString("N"));
    string SettingsPath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var settings = AppSettings.Load(SettingsPath);
        Assert.True(settings.Enabled);
        Assert.Empty(settings.Shortcuts);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var saved = new AppSettings { Enabled = false, Theme = AppTheme.Dark, ExcludedApps = ["mstsc.exe"] };
        saved.Shortcuts["edit.copy"] = false;
        saved.CustomShortcuts.Add(CustomShortcut.From(new Chord(Keys.K, Mods.Alt | Mods.Shift), new Chord(Keys.Home, Mods.Ctrl)));
        saved.Save(SettingsPath);

        var loaded = AppSettings.Load(SettingsPath);
        Assert.False(loaded.Enabled);
        Assert.Equal(AppTheme.Dark, loaded.Theme);
        Assert.Equal(["mstsc.exe"], loaded.ExcludedApps);
        Assert.False(loaded.Shortcuts["edit.copy"]);
        var custom = Assert.Single(loaded.CustomShortcuts);
        Assert.Equal(("Alt+Shift+K", "Ctrl+Home"), (custom.TriggerText, custom.SendsText));
    }

    [Fact]
    public void Save_LeavesNoTemporaryFile()
    {
        new AppSettings().Save(SettingsPath);
        new AppSettings().Save(SettingsPath);
        Assert.Equal(["settings.json"], Directory.GetFiles(_dir).Select(Path.GetFileName));
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndKeepsACopy()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{ \"Enabled\": fal");

        var settings = AppSettings.Load(SettingsPath);

        Assert.True(settings.Enabled);
        Assert.Equal("{ \"Enabled\": fal", File.ReadAllText(SettingsPath + ".corrupt"));
    }
}
