using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Virgil.App.Services;

public class JsonSettingsService : ISettingsService
{
    private readonly string _path;
    public JsonSettingsService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
    }
    public async Task<CleanOptions> LoadAsync()
    {
        try
        {
            if (!File.Exists(_path)) return new CleanOptions();
            var json = await File.ReadAllTextAsync(_path);
            return JsonSerializer.Deserialize<CleanOptions>(json) ?? new CleanOptions();
        } catch { return new CleanOptions(); }
    }
    public async Task SaveAsync(CleanOptions options)
    {
        var json = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_path, json);
    }
}
