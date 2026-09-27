using System.Text.Json;
using System.Text.Json.Serialization;

namespace MacShortcuts;

[JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
public enum AppTheme { System, Light, Dark }

public sealed class AppSettings
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacShortcuts", "settings.json");

    public bool Enabled { get; set; } = true;
    public bool UseLeftAlt { get; set; } = true;
    public bool UseRightAlt { get; set; } = true;
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Only shortcuts the user has toggled are stored; the rest use their defaults.</summary>
    public Dictionary<string, bool> Shortcuts { get; set; } = [];

    /// <summary>Process names (e.g. "mstsc.exe") where nothing is remapped.</summary>
    public List<string> ExcludedApps { get; set; } = [];

    public bool IsEnabled(ShortcutDef shortcut) =>
        Shortcuts.TryGetValue(shortcut.Id, out bool on) ? on : shortcut.DefaultEnabled;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
        }
        catch
        {
            // Corrupt settings file: fall back to defaults.
        }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public RemapConfig ToConfig()
    {
        var map = new Dictionary<Trigger, ShortcutAction>();
        foreach (var shortcut in ShortcutCatalog.All.Where(IsEnabled))
            foreach (var binding in shortcut.Bindings)
                map.TryAdd(binding.Trigger, binding.Action);

        var excluded = ExcludedApps
            .Select(a => a.Trim().ToLowerInvariant())
            .Select(a => a.EndsWith(".exe") ? a[..^4] : a)
            .Where(a => a.Length > 0)
            .ToHashSet();

        bool ctrlClick = ShortcutCatalog.All.Any(s => s.Id == ShortcutCatalog.MouseCtrlClickId && IsEnabled(s));

        return new RemapConfig(Enabled, UseLeftAlt, UseRightAlt, ctrlClick, map, excluded);
    }
}
