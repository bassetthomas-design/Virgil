using System;
using System.IO;
using System.Text.Json;

namespace Virgil.Services;

public interface IPerformanceStateStore
{
    PerformanceModeState Load();
    void Save(PerformanceModeState state);
    void Clear();
}

public sealed class FilePerformanceStateStore : IPerformanceStateStore
{
    private readonly string _path;

    public FilePerformanceStateStore(string? customPath = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var root = Path.Combine(appData, "Virgil");
        Directory.CreateDirectory(root);
        _path = customPath ?? Path.Combine(root, "performance-mode-state.json");
    }

    public PerformanceModeState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new PerformanceModeState();
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<PerformanceModeState>(json) ?? new PerformanceModeState();
        }
        catch
        {
            return new PerformanceModeState();
        }
    }

    public void Save(PerformanceModeState state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Best effort only.
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch
        {
            // Best effort only.
        }
    }
}

public record PerformanceModeState
{
    public bool IsActive { get; init; }
    public string? PreviousPowerPlanGuid { get; init; }
    public string? ActivePowerPlanGuid { get; init; }
    public int? PreviousPrioritySeparation { get; init; }
    public DateTimeOffset? ActivatedAt { get; init; }
}
