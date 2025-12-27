using System;
using System.Collections.Generic;
using System.Linq;

namespace Virgil.App.Services
{
    public sealed class ActionRegistry
    {
        private readonly Dictionary<string, ActionDefinition> _definitions;

        public ActionRegistry(IEnumerable<ActionDefinition> definitions)
        {
            if (definitions is null) throw new ArgumentNullException(nameof(definitions));
            _definitions = definitions.ToDictionary(d => d.Key, d => d, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyCollection<ActionDefinition> All => _definitions.Values;

        public bool TryGet(string key, out ActionDefinition? definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return _definitions.TryGetValue(key, out definition);
        }
    }
}
