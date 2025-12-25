using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Service that periodically emits a punchline or anecdote during monitoring.
    /// Punchlines can be customized and the service can be enabled/disabled.
    /// </summary>
    public class PunchlineService : IDisposable
    {
        private readonly List<string> _punchlines;
        private readonly Random _random = new();
        private CancellationTokenSource? _cts;
        private readonly object _sync = new();

        /// <summary>
        /// Event fired whenever a punchline is generated.
        /// </summary>
        public event Action<string>? PunchlineGenerated;

        /// <summary>
        /// When set to false, no punchlines will be emitted.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Minimum delay between punchlines. Default 1 minute.
        /// </summary>
        public TimeSpan MinInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Maximum delay between punchlines. Default 6 minutes.
        /// </summary>
        public TimeSpan MaxInterval { get; set; } = TimeSpan.FromMinutes(6);

        public PunchlineService(IEnumerable<string>? punchlines = null)
        {
            _punchlines = punchlines?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? GetDefaultPunchlines();
        }

        /// <summary>
        /// List of currently configured punchlines.
        /// </summary>
        public IReadOnlyList<string> Punchlines => _punchlines.AsReadOnly();

        /// <summary>
        /// Add a new punchline to the list.
        /// </summary>
        public void AddPunchline(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                _punchlines.Add(text);
        }

        /// <summary>
        /// Remove a punchline from the list.
        /// </summary>
        public bool RemovePunchline(string text)
        {
            return _punchlines.Remove(text);
        }

        /// <summary>
        /// Starts emitting punchlines asynchronously until Stop() is called or IsEnabled is set to false.
        /// </summary>
        public void Start()
        {
            if (!IsEnabled || _punchlines.Count == 0)
                return;

            lock (_sync)
            {
                // Cancel existing loop if any
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                _ = Task.Run(() => RunAsync(_cts.Token));
            }
        }

        /// <summary>
        /// Stops emitting punchlines.
        /// </summary>
        public void Stop()
        {
            lock (_sync)
            {
                _cts?.Cancel();
                _cts = null;
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var delay = MinInterval + TimeSpan.FromMilliseconds(_random.NextDouble() * (MaxInterval - MinInterval).TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var idx = _random.Next(_punchlines.Count);
                    var text = _punchlines[idx];
                    PunchlineGenerated?.Invoke(text);
                }
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }

        private static List<string> GetDefaultPunchlines()
        {
            return new List<string>
            {
                "Il est temps de faire une pause !",
                "Saviez-vous que l'ordinateur a besoin de caféine ?",
                "Encore une mission accomplie.",
                "Votre système est propre comme un sou neuf !"
            };
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
