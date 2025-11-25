using System.Collections.Generic;

namespace Virgil.Domain
{
    /// <summary>
    /// Represents a single phrase that Virgil can say in the chat.
    /// Backed by the data stored in VirgilPhrases.json.
    /// </summary>
    public class VirgilPhrase
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Tone { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public string Text { get; set; } = string.Empty;
    }
}
