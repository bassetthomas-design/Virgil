using System;
using Serilog;
using Serilog.Events;

namespace Virgil.Core.Services
{
    public static class LoggingService
    {
        private static bool _initialized;

        public static void Init(LogEventLevel level = LogEventLevel.Information)
        {
            if (_initialized) return;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(level)
                .WriteTo.File(
                    path: "Logs\\virgil-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 10,
                    shared: true)
                .CreateLogger();

            _initialized = true;
            SafeInfo("Serilog initialized.");
        }

        public static void SafeInfo(string template, params object[] args)
        {
            if (!_initialized) return;
            try { Log.Information(template, args); } catch { /* ignore */ }
        }

        public static void SafeError(Exception ex, string template, params object[] args)
        {
            if (!_initialized) return;
            try { Log.Error(ex, template, args); } catch { /* ignore */ }
        }
    }
}
