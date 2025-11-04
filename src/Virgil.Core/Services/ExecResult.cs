using System;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Résultat d'une exécution de commande/process.
    /// </summary>
    public sealed class ExecResult
    {
        public bool Success { get; init; }
        public int ExitCode { get; init; }
        public string StdOut { get; init; } = string.Empty;
        public string StdErr { get; init; } = string.Empty;
        public TimeSpan Duration { get; init; }

        public static ExecResult From(int exitCode, string stdout, string stderr, TimeSpan duration)
            => new ExecResult
            {
                Success = exitCode == 0,
                ExitCode = exitCode,
                StdOut = stdout ?? string.Empty,
                StdErr = stderr ?? string.Empty,
                Duration = duration
            };
    }
}
