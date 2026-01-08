using System.Collections.Generic;

namespace Virgil.App.Chat
{
    public sealed class ProposedActionItem
    {
        public ProposedActionItem(string actionId, string title, Dictionary<string, string>? parameters, string? warning)
        {
            ActionId = actionId;
            Title = title;
            Parameters = parameters;
            Warning = warning;
        }

        public string ActionId { get; }

        public string Title { get; }

        public Dictionary<string, string>? Parameters { get; }

        public string? Warning { get; }

        public string ButtonLabel => $"Exécuter {Title}";
    }
}
