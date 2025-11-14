using System;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Implémentation minimale de IChatService pour le câblage de la chaîne d’actions.
/// Cette version se contente d’écrire dans la console – à remplacer plus tard par le vrai Chat UI.
/// </summary>
public sealed class ChatService : IChatService
{
    public Task InfoAsync(string message, CancellationToken ct = default)
    {
        WriteTagged("INFO", message);
        return Task.CompletedTask;
    }

    public Task WarnAsync(string message, CancellationToken ct = default)
    {
        WriteTagged("WARN", message);
        return Task.CompletedTask;
    }

    public Task ErrorAsync(string message, CancellationToken ct = default)
    {
        WriteTagged("ERROR", message);
        return Task.CompletedTask;
    }

    public Task ThanosWipeAsync(bool preservePinned = true, CancellationToken ct = default)
    {
        WriteTagged("THANOS", preservePinned
            ? "Wipe du chat (messages épinglés préservés)."
            : "Wipe du chat complet.");
        return Task.CompletedTask;
    }

    private static void WriteTagged(string level, string message)
    {
        Console.WriteLine($"[Virgil:{level}] {message}");
    }
}
