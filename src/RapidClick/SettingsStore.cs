using System.IO;
using System.Text.Json;
using RapidClick.Core;

namespace RapidClick;

internal static class SettingsStore
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RapidClick");
    public static ClickOptions Load()
    {
        try
        {
            var options = JsonSerializer.Deserialize<ClickOptions>(File.ReadAllText(Path.Combine(Folder, "settings.json"))) ?? new();
            options.Validate(); return options;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return new(); }
    }
    public static void Save(ClickOptions options)
    {
        options.Validate(); Directory.CreateDirectory(Folder);
        string destination = Path.Combine(Folder, "settings.json"), temporary = destination + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, destination, true);
    }
}
