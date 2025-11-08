using System.Text.Json;
using System.Text.Json.Serialization;

var outDir = args.Length>0? args[0] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "src", "Virgil.App", "assets", "voice", "fr", "generated");
Directory.CreateDirectory(outDir);

var rnd = new Random(1337);

// Small template dictionaries; can be extended easily.
string[] openings = { "OK", "C'est parti", "Hop", "Top chrono", "Banco" };
string[] cleans = { "je vide les temporaires", "je rince les caches", "je souffle sur les poussières digitales", "je défragmente ton karma" };
string[] updates = { "je mets tout à jour", "je rafraîchis apps et pilotes", "je passe un coup sur Windows Update" };
string[] closers = { "nickel chrome", "c'est propre", "ça respire", "tout roule" };
string[] refs90 = { "comme un Walkman neuf", "plus smooth que Tony Hawk 2", "vite comme une cartouche SNES", "sans lag façon modem 56k" };
string[] moods = { "happy", "focused", "warn", "alert", "sleepy" };
string[] acts = { "Game", "Browser", "IDE", "Office", "Media", "Terminal" };

var buckets = new List<(string key,string text,int w,string[]? moods,string[]? acts,int? minHour,int? maxHour,int? cooldown)>();

// Generate action lines
for (int i=0;i<1500;i++){
    var t = $"{openings[rnd.Next(openings.Length)]}, {cleans[rnd.Next(cleans.Length)]}... {closers[rnd.Next(closers.Length)]}.";
    buckets.Add(($"gen.clean.{i}", t, 1, new[]{moods[rnd.Next(moods.Length)]}, null, null, null, rnd.Next(30,300)));
}
for (int i=0;i<1500;i++){
    var t = $"{openings[rnd.Next(openings.Length)]}, {updates[rnd.Next(updates.Length)]} ({refs90[rnd.Next(refs90.Length)]}).";
    buckets.Add(($"gen.update.{i}", t, 1, null, new[]{acts[rnd.Next(acts.Length)]}, null, null, rnd.Next(60,600)));
}

// Punchlines generic
for (int i=0;i<5000;i++){
    var t = $"{refs90[rnd.Next(refs90.Length)]}.";
    buckets.Add(($"gen.punch.{i}", t, 1, null, null, null, null, rnd.Next(300,1200)));
}

// Reactions
for (int i=0;i<1000;i++){
    var t = $"Encore ? J'insiste: {refs90[rnd.Next(refs90.Length)]}.";
    buckets.Add(($"gen.react.{i}", t, 1, null, null, null, null, 120));
}

// Time based
for (int i=0;i<1000;i++){
    var h = rnd.Next(0,24); var span = rnd.Next(1,3);
    var t = h<6? "Nuit noire: maintenance discrète." : (h<12? "Matin à donf." : (h<18? "Après-midi: je garde le rythme." : "Soirée tranquille, je veille."));
    buckets.Add(($"gen.time.{i}", t, 1, null, null, h, Math.Min(23,h+span), rnd.Next(300,900)));
}

// Split into shards of ~1000 entries each
int shardSize = 1000;
int shardIndex = 1;
foreach (var chunk in buckets.Chunk(shardSize))
{
    var dict = new Dictionary<string, List<object>>();
    foreach (var (k, t, w, ms, acs, minH, maxH, cd) in chunk)
    {
        if (!dict.TryGetValue(k, out var list)) dict[k] = list = new();
        var entry = new Dictionary<string, object?>
        {
            ["t"] = t,
            ["w"] = w
        };
        if (ms!=null) entry["moods"] = ms;
        if (acs!=null) entry["activities"] = acs;
        if (minH.HasValue) entry["minHour"] = minH.Value;
        if (maxH.HasValue) entry["maxHour"] = maxH.Value;
        if (cd.HasValue) entry["cooldownSec"] = cd.Value;
        list.Add(entry);
    }

    var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions{ WriteIndented = true });
    var path = Path.Combine(outDir, $"generated.{shardIndex:D2}.json");
    File.WriteAllText(path, json);
    shardIndex++;
}

Console.WriteLine($"Generated {buckets.Count} entries into {outDir}");
