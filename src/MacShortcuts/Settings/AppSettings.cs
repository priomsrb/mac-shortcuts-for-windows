using System.Text.Json;
using System.Text.Json.Serialization;
using MacShortcuts.Remapping;
using MacShortcuts.Shortcuts;

namespace MacShortcuts.Settings;

public sealed class AppSettings
{
    static readonly string DefaultPath = Path.Combine(
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

    public static AppSettings Load() => Load(DefaultPath);

    public void Save() => Save(DefaultPath);

    /// <summary>Returns defaults if the file is missing or unreadable.</summary>
    internal static AppSettings Load(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Includes a missing file or folder, i.e. the first run.
            return new();
        }

        try
        {
            return JsonSerializer.Deserialize(json, SettingsJson.Default.AppSettings) ?? new();
        }
        catch (JsonException)
        {
            // Keep a copy for the user; the next save overwrites the original with defaults.
            try { File.Copy(path, path + ".corrupt", overwrite: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return new();
        }
    }

    internal void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Write a temporary file and swap it in, so a crash mid-write can't leave a truncated file.
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, SettingsJson.Default.AppSettings));
        File.Move(temp, path, overwrite: true);
    }

    public RemapConfig ToConfig()
    {
        var map = new Dictionary<Trigger, ShortcutAction>();
        foreach (var shortcut in ShortcutCatalog.All.Where(IsEnabled))
            foreach (var binding in shortcut.Bindings)
                map.TryAdd(binding.Trigger, binding.Action);

        var excluded = ExcludedApps
            .Select(a => a.Trim().ToLowerInvariant())
            .Select(a => a.EndsWith(".exe", StringComparison.Ordinal) ? a[..^4] : a)
            .Where(a => a.Length > 0)
            .ToHashSet();

        bool ctrlClick = IsEnabled(ShortcutCatalog.CtrlClick);
        bool ctrlScroll = IsEnabled(ShortcutCatalog.CtrlScroll);

        return new RemapConfig(Enabled, UseLeftAlt, UseRightAlt, ctrlClick, ctrlScroll, map, excluded);
    }
}

/// <summary>Source-generated serializer, since Native AOT has no reflection-based JSON.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJson : JsonSerializerContext;
