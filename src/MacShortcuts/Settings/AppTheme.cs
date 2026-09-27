using System.Text.Json.Serialization;

namespace MacShortcuts.Settings;

[JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
public enum AppTheme { System, Light, Dark }
