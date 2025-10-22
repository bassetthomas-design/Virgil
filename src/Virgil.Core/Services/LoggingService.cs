using System;
using Serilog;
using Serilog.Events;
using Virgil.Core.Config;

namespace Virgil.Core.Services
{
    public static class LoggingService
    {
        private static bool _inited;

        public static void Init(LogEventLevel minLevel = LogEventLevel.Information)
        {
            if (_inited) return;

            var logPathPattern = AppPaths.LogFile; // e.g. ...\virgil-.log
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(minLevel)
                .WriteTo.File(
                    logPathPattern,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    shared: true,
                    fileSizeLimitBytes: 10_000_000,
                    rollOnFileSizeLimit: true)
                .CreateLogger();

            _inited = true;
            Log.Information("Serilog initialized (level {Level})", minLevel);
        }

        public static void SetLevel(LogEventLevel level)
        {
            // Serilog ne permet pas de changer dynamiquement sans relancer le logger;
            // par simplicité on réinitialise.
            Log.CloseAndFlush();
            _inited = false;
            Init(level);
        }

        public static void SafeInfo(string msg, params object[] args)
        {
            try { Log.Information(msg, args); } catch { }
        }

        public static void SafeError(Exception ex, string msg, params object[] args)
        {
            try { Log.Error(ex, msg, args); } catch { }
        }
    }
}
