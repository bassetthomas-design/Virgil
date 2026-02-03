using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Logging;
using Virgil.Core.Models;

namespace Virgil.Core.Services
{
    public sealed class WindowsUpdateService
    {
        private const int OperationSucceeded = 2;
        private const int OperationSucceededWithErrors = 3;

        public Task<WindowsUpdateResult> RunAsync(WindowsUpdateOptions options, IProgress<double>? progress, CancellationToken ct)
        {
            options ??= new WindowsUpdateOptions();
            return Task.Run(() => RunInternal(options, progress, ct), ct);
        }

        private static WindowsUpdateResult RunInternal(WindowsUpdateOptions options, IProgress<double>? progress, CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(0);

                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (sessionType is null)
                {
                    return Failure("Service Windows Update indisponible.");
                }

                dynamic session = Activator.CreateInstance(sessionType) ?? throw new InvalidOperationException("WUA session indisponible.");
                dynamic searcher = session.CreateUpdateSearcher();
                searcher.Online = true;

                var criteria = BuildCriteria(options);
                dynamic searchResult = searcher.Search(criteria);
                dynamic updates = searchResult.Updates;
                var updatesFound = (int)updates.Count;

                if (updatesFound == 0)
                {
                    return new WindowsUpdateResult(true, 0, 0, false, "Rien à installer.", null);
                }

                if (options.SearchOnly)
                {
                    return new WindowsUpdateResult(true, updatesFound, 0, false, "Recherche terminée.", null);
                }

                ct.ThrowIfCancellationRequested();
                progress?.Report(20);

                dynamic downloader = session.CreateUpdateDownloader();
                downloader.Updates = updates;
                dynamic downloadResult = downloader.Download();

                if (!IsSuccess(downloadResult.ResultCode))
                {
                    var failureReason = BuildFailureReason("Téléchargement Windows Update échoué.", TryGetHresult(downloadResult));
                    Log.Error($"Windows Update download failed: {failureReason}");
                    return Failure("Téléchargement Windows Update échoué.", updatesFound, 0, false, failureReason);
                }

                progress?.Report(60);

                dynamic downloadedUpdates = CreateUpdateCollection(updates, onlyDownloaded: true);
                var downloadedCount = (int)downloadedUpdates.Count;
                if (downloadedCount == 0)
                {
                    var failureReason = BuildFailureReason("Aucune mise à jour téléchargée.", null);
                    Log.Error($"Windows Update download failed: {failureReason}");
                    return Failure("Aucune mise à jour téléchargée.", updatesFound, 0, false, failureReason);
                }

                dynamic installer = session.CreateUpdateInstaller();
                installer.Updates = downloadedUpdates;
                dynamic installResult = installer.Install();

                var rebootRequired = installResult.RebootRequired is bool flag && flag;
                var updatesInstalled = CountInstalled(downloadedUpdates);

                progress?.Report(100);

                if (!IsSuccess(installResult.ResultCode))
                {
                    var failureReason = BuildFailureReason("Installation Windows Update échouée.", TryGetHresult(installResult));
                    Log.Error($"Windows Update install failed: {failureReason}");
                    return Failure("Installation Windows Update échouée.", updatesFound, updatesInstalled, rebootRequired, failureReason);
                }

                if (updatesInstalled == 0)
                {
                    var failureReason = BuildFailureReason("Aucune mise à jour installée.", null);
                    Log.Error($"Windows Update install failed: {failureReason}");
                    return Failure("Aucune mise à jour installée.", updatesFound, 0, rebootRequired, failureReason);
                }

                return new WindowsUpdateResult(true, updatesFound, updatesInstalled, rebootRequired, "Windows Update terminé.", null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (COMException ex)
            {
                var hresult = ex.HResult;
                var failureReason = BuildFailureReason(ex.Message, hresult);
                Log.Error($"Windows Update COM error: 0x{hresult:X8} {ex.Message}");
                return Failure("Erreur Windows Update.", 0, 0, false, failureReason);
            }
            catch (Exception ex)
            {
                Log.Error($"Windows Update error: {ex.Message}");
                return Failure("Erreur Windows Update.", 0, 0, false, BuildFailureReason(ex.Message, null));
            }
        }

        private static string BuildCriteria(WindowsUpdateOptions options)
        {
            var baseCriteria = "IsInstalled=0 and IsHidden=0";
            return options.IncludeDrivers ? baseCriteria : $"{baseCriteria} and Type='Software'";
        }

        private static bool IsSuccess(object resultCode)
        {
            var code = Convert.ToInt32(resultCode);
            return code == OperationSucceeded || code == OperationSucceededWithErrors;
        }

        private static int CountInstalled(dynamic updates)
        {
            var installed = 0;
            var count = (int)updates.Count;
            for (var i = 0; i < count; i++)
            {
                dynamic update = updates.Item(i);
                if (update.IsInstalled is bool flag && flag)
                {
                    installed++;
                }
            }

            return installed;
        }

        private static dynamic CreateUpdateCollection(dynamic updates, bool onlyDownloaded)
        {
            var collectionType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");
            if (collectionType is null)
            {
                return updates;
            }

            dynamic collection = Activator.CreateInstance(collectionType) ?? updates;
            var count = (int)updates.Count;
            for (var i = 0; i < count; i++)
            {
                dynamic update = updates.Item(i);
                if (!onlyDownloaded || (update.IsDownloaded is bool downloaded && downloaded))
                {
                    collection.Add(update);
                }
            }

            return collection;
        }

        private static int? TryGetHresult(dynamic result)
        {
            try
            {
                var hresult = (int)result.HResult;
                return hresult == 0 ? null : hresult;
            }
            catch
            {
                return null;
            }
        }

        private static WindowsUpdateResult Failure(string summary, int updatesFound = 0, int updatesInstalled = 0, bool rebootRequired = false, string? failureReason = null)
            => new(false, updatesFound, updatesInstalled, rebootRequired, summary, failureReason);

        private static string BuildFailureReason(string message, int? hresult)
        {
            var resolvedHresult = hresult ?? 0;
            return $"last_hresult=0x{resolvedHresult:X8}; last_error_message={message}";
        }
    }
}
