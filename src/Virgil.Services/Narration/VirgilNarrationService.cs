using System;
using System.Threading;
using System.Threading.Tasks;
using Virgil.App.Chat;
using Virgil.Domain;

namespace Virgil.Services.Narration
{
    /// <summary>
    /// High-level narration orchestrator for Virgil.
    /// It bridges domain phrase selection (VirgilPhraseService) with the
    /// chat output (IChatService), so that other services can just signal
    /// events (startup, actions, service state changes, ambient) and let
    /// Virgil speak in the chat box.
    /// </summary>
    public class VirgilNarrationService
    {
        private readonly IChatService _chat;
        private readonly VirgilPhraseService _phrases;
        private readonly Random _random = new();

        public VirgilNarrationService(IChatService chat, VirgilPhraseService phrases)
        {
            _chat = chat ?? throw new ArgumentNullException(nameof(chat));
            _phrases = phrases ?? throw new ArgumentNullException(nameof(phrases));
        }

        /// <summary>
        /// Called when the application starts up and Virgil should announce
        /// that everything is ready.
        /// </summary>
        public async Task OnStartupAsync(CancellationToken cancellationToken = default)
        {
            var phrase = _phrases.GetRandomStartup()
                         ?? _phrases.GetRandomAmbient()
                         ?? _phrases.GetRandom();

            if (phrase is null)
            {
                return;
            }

            await _chat.SendAsync(phrase.Text, cancellationToken);
        }

        /// <summary>
        /// Called when a user-triggered action starts (scan, cleanup, etc.).
        /// </summary>
        public async Task OnActionStartedAsync(string actionId, CancellationToken cancellationToken = default)
        {
            var phrase = _phrases.GetRandomActionStart()
                         ?? _phrases.GetRandom("action_start")
                         ?? _phrases.GetRandomAmbient();

            if (phrase is null)
            {
                return;
            }

            await _chat.SendAsync(phrase.Text, cancellationToken);
        }

        /// <summary>
        /// Called when an action finishes (success or failure).
        /// </summary>
        public async Task OnActionCompletedAsync(string actionId, bool success, CancellationToken cancellationToken = default)
        {
            var phrase = _phrases.GetRandomActionEnd()
                         ?? _phrases.GetRandom("action_end")
                         ?? _phrases.GetRandomAmbient();

            if (phrase is null)
            {
                return;
            }

            await _chat.SendAsync(phrase.Text, cancellationToken);
        }

        /// <summary>
        /// Called when a monitored service changes state. The stateLabel is a
        /// simple string description (e.g. "Ok", "Warning", "Error").
        /// </summary>
        public async Task OnServiceStateChangedAsync(string serviceName, string stateLabel, CancellationToken cancellationToken = default)
        {
            VirgilPhrase? phrase = stateLabel switch
            {
                "Warning" => _phrases.GetRandomServiceWarning(),
                "Error" => _phrases.GetRandomServiceWarning(),
                _ => _phrases.GetRandomServiceOk(),
            } ?? _phrases.GetRandomAmbient();

            if (phrase is null)
            {
                return;
            }

            await _chat.SendAsync(phrase.Text, cancellationToken);
        }

        /// <summary>
        /// Starts an ambient narration loop: every 1 to 6 minutes (random),
        /// Virgil will say something in the chat if there is at least one
        /// suitable phrase available. The caller is responsible for managing
        /// the provided cancellation token.
        /// </summary>
        public Task StartAmbientLoopAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var delaySeconds = _random.Next(60, 6 * 60 + 1); // 1-6 minutes

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var phrase = _phrases.GetRandomAmbient()
                                 ?? _phrases.GetRandomThps2()
                                 ?? _phrases.GetRandom();

                    if (phrase is null)
                    {
                        continue;
                    }

                    await _chat.SendAsync(phrase.Text, cancellationToken);
                }
            }, cancellationToken);
        }
    }
}
