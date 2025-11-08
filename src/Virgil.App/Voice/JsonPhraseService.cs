using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Virgil.App.Voice;

public class JsonPhraseService : IPhraseService
{
    private readonly Dictionary<string, List<Entry>> _map = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastUse = new();
    private readonly Queue<string> _recent = new();
    private const int RecentMax = 50;
    private static readonly Regex Placeholder = new("\{(a|c|e|[a-zA-Z]+)\}");

    public JsonPhraseService(string lang = "fr")
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = Path.Combine(baseDir, "assets", "voice", lang);
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            var json = File.ReadAllText(file);
            var dict = JsonSerializer.Deserialize<Dictionary<string, List<Entry>>>(json);
            if (dict == null) continue;
            foreach (var (k, list) in dict)
            {
                if (!_map.TryGetValue(k, out var bucket)) _map[k] = bucket = new();
                bucket.AddRange(list);
            }
        }
    }

    public string Get(string key, PhraseContext ctx)
    {
        if (!_map.TryGetValue(key, out var list) || list.Count == 0) return key;
        var now = DateTime.UtcNow;
        var filtered = list.Where(e => Allowed(e, ctx, now)).ToList();
        if (filtered.Count == 0) filtered = list;
        var pick = WeightedPick(filtered);
        Touch(key);
        return Interpolate(pick.t, ctx);
    }

    public string? TryGetRandom(string bucket, PhraseContext ctx)
    {
        var keys = _map.Keys.Where(k => k.StartsWith(bucket + ".", StringComparison.OrdinalIgnoreCase)).ToList();
        if (keys.Count == 0) return null;
        var rnd = new Random();
        foreach (var k in keys.OrderBy(_ => rnd.Next()))
        {
            var s = Get(k, ctx);
            if (!string.Equals(s, k, StringComparison.Ordinal)) return s;
        }
        return null;
    }

    private static Entry WeightedPick(List<Entry> list)
    {
        var rnd = new Random();
        var sum = list.Sum(e => Math.Max(1, e.w));
        var r = rnd.Next(1, sum + 1);
        var acc = 0;
        foreach (var e in list) { acc += Math.Max(1, e.w); if (acc >= r) return e; }
        return list[^1];
    }

    private bool Allowed(Entry e, PhraseContext ctx, DateTime now)
    {
        if (e.minHour.HasValue && ctx.Hour < e.minHour.Value) return false;
        if (e.maxHour.HasValue && ctx.Hour > e.maxHour.Value) return false;
        if (e.cooldownSec.HasValue)
        {
            var key = e.t;
            if (_lastUse.TryGetValue(key, out var last))
            {
                if ((now - last).TotalSeconds < e.cooldownSec.Value) return false;
            }
        }
        if (e.moods != null && e.moods.Length > 0 && !e.moods.Contains(ctx.Mood)) return false;
        if (e.activities != null && e.activities.Length > 0 && !e.activities.Contains(ctx.Activity)) return false;
        if (_recent.Contains(e.t)) return false;
        return true;
    }

    private void Touch(string text)
    {
        _lastUse[text] = DateTime.UtcNow;
        _recent.Enqueue(text);
        while (_recent.Count > RecentMax) _recent.Dequeue();
    }

    private static string Interpolate(string s, PhraseContext c)
    {
        return s
            .Replace("{cpu}", c.Cpu.ToString("F0"))
            .Replace("{ram}", c.Ram.ToString("F0"))
            .Replace("{temp}", c.Temp.ToString("F0"))
            .Replace("{files}", c.Files.ToString())
            .Replace("{mb}", (c.Bytes/1024.0/1024.0).ToString("F0"))
            .Replace("{percent}", c.Percent.ToString())
            .Replace("{action}", c.Action)
            .Replace("{mood}", c.Mood)
            .Replace("{activity}", c.Activity);
    }

    public class Entry
    {
        public string t { get; set; } = string.Empty;
        public int w { get; set; } = 1;
        public int? cooldownSec { get; set; }
        public string[]? moods { get; set; }
        public string[]? activities { get; set; }
        public int? minHour { get; set; }
        public int? maxHour { get; set; }
    }
}
