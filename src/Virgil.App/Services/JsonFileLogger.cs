using System;
using System.IO;
using System.Text.Json;

namespace Virgil.App.Services;

public class JsonFileLogger : IJsonLogger
{
    private readonly string _dir;
    private readonly object _sync = new();
    public JsonFileLogger()
    {
        _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "logs");
        Directory.CreateDirectory(_dir);
    }
    public void Write(object evt)
    {
        var path = Path.Combine(_dir, DateTime.Now.ToString("yyyy-MM-dd") + ".json");
        var line = JsonSerializer.Serialize(evt);
        lock(_sync) File.AppendAllText(path, line + Environment.NewLine);
    }
}
