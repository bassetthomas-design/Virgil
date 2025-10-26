using System.Collections.Generic;

namespace Virgil.Core.Services
{
    public record MoodDescriptor(string EyesKey, string ColorHex);

    /// <summary>
    /// Default moods mapped to an eye-style key and a face color (eyes-only avatar).
    /// Non-breaking: additive only.
    /// </summary>
    public static class MoodCatalog
    {
        public static readonly IReadOnlyDictionary<string, MoodDescriptor> Defaults =
            new Dictionary<string, MoodDescriptor>
            {
                ["neutral"]   = new("neutral",  "#5A8F3F"),
                ["happy"]     = new("smile",    "#5A8F3F"),
                ["relaxed"]   = new("relaxed",  "#5A8F3F"),
                ["sleepy"]    = new("sleepy",   "#5A8F3F"),
                ["sad"]       = new("tear",     "#5A8F3F"),
                ["thinking"]  = new("side",     "#5A8F3F"),
                ["blink"]     = new("blink",    "#5A8F3F"),
                ["surprised"] = new("round",    "#5A8F3F"),
                ["inlove"]    = new("hearts",   "#FF66A6"),
                ["cat"]       = new("cat",      "#FFFFFF"),
                ["devil"]     = new("angry",    "#E53935"),
                ["angry"]     = new("angry",    "#E53935"),
                ["smirk"]     = new("smirk",    "#5A8F3F"),
                ["wink"]      = new("wink",     "#5A8F3F"),
                ["focus"]     = new("focus",    "#5A8F3F"),
                ["confused"]  = new("confused", "#5A8F3F"),
                ["sly"]       = new("sly",      "#5A8F3F"),
                ["joy"]       = new("joy",      "#5A8F3F"),
                ["neutral2"]  = new("neutral2", "#5A8F3F")
            };
    }
}
