using System.IO;
using System.Text.Json;
using SnapStack.Models;

namespace SnapStack.Services;

/// <summary>
/// 설정 영속 구현 — %APPDATA%\SnapStack\settings.json. §5 SYS-05.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private readonly string _path;

    public AppSettings Current { get; private set; } = new();

    public SettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SnapStack");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
        }
        catch
        {
            // 손상 시 기본값으로 복구(SYS-05).
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, JsonOpts));
        }
        catch { /* 저장 실패 무시(다음 시도) */ }
    }
}
