using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Virgil.App.Models;

namespace Virgil.App.Services
{
    public sealed class ActionDefinition
    {
        public ActionDefinition(
            string key,
            string displayName,
            bool isDestructive,
            Func<Dictionary<string, string>?, CancellationToken, Task<ActionResult>> executeAsync)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            IsDestructive = isDestructive;
            ExecuteAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        }

        public string Key { get; }

        public string DisplayName { get; }

        public bool IsDestructive { get; }

        public Func<Dictionary<string, string>?, CancellationToken, Task<ActionResult>> ExecuteAsync { get; }
    }
}
