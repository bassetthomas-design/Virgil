using System;
using System.Timers;

namespace Virgil.App.Services
{
    public class BeatPulseService : IDisposable
    {
        public event Action<double>? Pulse; // intensity 0..1
        private readonly Timer _t;
        private readonly Random _rng = new();
        public BeatPulseService(int baseMs = 900)
        {
            _t = new Timer(baseMs) { AutoReset = true };
            _t.Elapsed += (_, __) =>
            {
                // small jitter and gentle intensity
                var jitter = 0.85 + _rng.NextDouble() * 0.3;
                _t.Interval = Math.Max(400, baseMs * jitter);
                Pulse?.Invoke(0.12 + _rng.NextDouble() * 0.08);
            };
        }
        public void Start() => _t.Start();
        public void Stop() => _t.Stop();
        public void Dispose() { _t.Dispose(); }
    }
}
