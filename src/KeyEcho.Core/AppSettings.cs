using System.Text.Json;

namespace KeyEcho.Core;

public sealed record AppSettings
{
    public string PresetId { get; init; } = PresetCatalog.DefaultKeyboard;
    public float Volume { get; init; } = 0.35f;
    public bool Enabled { get; init; } = true;
    public bool MouseEnabled { get; init; } = true;
    public float MouseVolume { get; init; } = 0.35f;
    public string MousePresetId { get; init; } = "crisp";
    public bool StartOnLogin { get; init; }
    public string? CustomFile { get; init; }
    public string? CustomName { get; init; }

    public AppSettings Normalize() => this with
    {
        PresetId = PresetCatalog.IsKeyboard(PresetId) || PresetId is "pistol" or "rifle" or "shotgun" or "awm" or "fah" or "bing" or "bruh" or "augh" or "metal-pipe" or "shocked" or "private-laugh" or "wow" or "custom" ? PresetId : PresetCatalog.DefaultKeyboard,
        Volume = float.IsFinite(Volume) ? Math.Clamp(Volume, 0, 1) : 0.35f,
        MouseVolume = float.IsFinite(MouseVolume) ? Math.Clamp(MouseVolume, 0, 1) : 0.35f,
        MousePresetId = MousePresetId is "crisp" or "soft" or "follow" ? MousePresetId : "crisp"
    };
}

public sealed class SettingsStore(string directory)
{
    public string DirectoryPath { get; } = directory;
    public string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public AppSettings Load()
    {
        try { return (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new()).Normalize(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings.Normalize(), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
}
