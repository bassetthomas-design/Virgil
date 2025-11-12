using System;

namespace Virgil.App.Chat
{
    public static class PhraseEngine
    {
        private static readonly Random Rng = new();
        public static string Pick(params string[] options) => options.Length == 0 ? string.Empty : options[Rng.Next(options.Length)];
    }
}
