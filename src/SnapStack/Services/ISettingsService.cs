using SnapStack.Models;

namespace SnapStack.Services;

/// <summary>설정 영속(§5 SYS-04/05). %APPDATA%\SnapStack\settings.json.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }
    void Load();
    void Save();
}
