using System.Text.Json;

namespace DisplayDivider.Core;

/// <summary>레이아웃/설정의 JSON 저장소. 기본 위치: %APPDATA%\DisplayDivider\layout.json</summary>
public static class LayoutStore
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DisplayDivider", "layout.json");

    public static LayoutConfig Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<LayoutConfig>(File.ReadAllText(path), Options) ?? new LayoutConfig();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // 손상된 파일은 무시하고 기본값으로 시작한다. 저장 시 덮어쓴다.
        }
        return new LayoutConfig();
    }

    public static void Save(LayoutConfig config, string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
    }
}
