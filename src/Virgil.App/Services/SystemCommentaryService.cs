using System;
using System.Threading;
using Virgil.App.Chat;
using Virgil.App.Services;

namespace Virgil.App.Services
{
    /// <summary>
    /// Emits contextual chat messages based on system activity. It subscribes to
    /// monitoring metrics and posts a humorous or professional comment when the
    /// system is under high load or very idle. A minimal interval prevents
    /// message spam. The tone can be toggled via the <see cref="UseHumor"/> property.
    /// See issue #149 for requirements.
    /// </summary>
    public sealed class SystemCommentaryService
    {
        private readonly MonitoringService _monitoring;
        private readonly ChatService _chat;
        private readonly Random _rnd = new();
        private DateTime _nextAllowed = DateTime.MinValue;

        /// <summary>
        /// Minimum interval between two comments. Default is 1 minute.
        /// </summary>
        public TimeSpan MinimumInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Maximum interval used to randomise the next comment time. Default is 6 minutes.
        /// </summary>
        public TimeSpan MaximumInterval { get; set; } = TimeSpan.FromMinutes(6);

        /// <summary>
        /// Switch between humorous and serious tone. True for humorous by default.
        /// </summary>
        public bool UseHumor { get; set; } = true;

        public SystemCommentaryService(MonitoringService monitoring, ChatService chat)
        {
            _monitoring = monitoring;
            _chat = chat;
            _monitoring.Metrics += OnMetrics;
        }

        private void OnMetrics(double cpuUsage, double gpuUsage, double ramUsage, double cpuTemp)
        {
            var now = DateTime.Now;
            if (now < _nextAllowed) return;
            // Determine event type
            string? evt = null;
            if (cpuUsage > 80 || gpuUsage > 80 || ramUsage > 80)
                evt = "high";
            else if (cpuUsage < 5 && gpuUsage < 5 && ramUsage < 30)
                evt = "idle";
            else
                return;
            // Prepare next allowed time with jitter
            var min = MinimumInterval;
            var max = MaximumInterval;
            if (max < min) max = min;
            int minMs = (int)min.TotalMilliseconds;
            int maxMs = (int)max.TotalMilliseconds;
            int delayMs = _rnd.Next(minMs, maxMs + 1);
            _nextAllowed = now.AddMilliseconds(delayMs);
            // Choose message
            var messages = UseHumor ? GetHumorous(evt) : GetSerious(evt);
            if (messages.Length == 0) return;
            var msg = messages[_rnd.Next(messages.Length)];
            _chat.PostSystemMessage(msg, MessageType.Info, ChatKind.Info);
        }

        private static string[] GetHumorous(string evt)
        {
            return evt switch
            {
                "high" => new[]
                {
                    "Ouf, ça chauffe ici ! 🔥",
                    "La machine tourne à plein régime 🚀",
                    "CPU en mode sauna 🧖‍♂þ"
                },
                "idle" => new[]
                {
                    "C'est le calme plat… 😴",
                    "Rien à signaler, tout est paisible.",
                    "On pourrait presque s'endormir."
                },
                _ => Array.Empty<string>()
            };
        }

        private static string[] GetSerious(string evt)
        {
            return evt switch
            {
                "high" => new[]
                {
                    "Charge élevée détectée.",
                    "Attention : utilisation importante des ressources.",
                    "Surveillance : charge système élevée."
                },
                "idle" => new[]
                {
                    "Activité faible observée.",
                    "Système en veille, tout est stable.",
                    "Période d'inactivité prolongée."
                },
                _ => Array.Empty<string>()
            };
        }
    }
}
