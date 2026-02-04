using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Logging;
using Virgil.Core.Models;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Service pour gérer les mises à jour de pilotes via Windows Update (WUA).
    /// </summary>
    public sealed class DriverUpdateService
    {
        private readonly IProgress<double>? _progress;

        private const int OperationSucceeded = 2;
        private const int OperationSucceededWithErrors = 3;

        public DriverUpdateService(IProgress<double>? progress = null)
        {
            _progress = progress;
        }

        public Task<DriverUpdateResult> ScanAsync(CancellationToken ct)
            => Task.Run(() => ScanInternal(ct), ct);

        public Task<DriverUpdateResult> InstallAsync(IReadOnlyList<DriverUpdateItem> items, CancellationToken ct)
            => Task.Run(() => InstallInternal(items, ct), ct);

        private DriverUpdateResult ScanInternal(CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                _progress?.Report(0);

                var session = CreateSession();
                if (session is null)
                {
                    return Failure("Service Windows Update indisponible.");
                }

                var updates = SearchDriverUpdates(session, out bool usedFallback);
                if (updates is null)
                {
                    return Failure("Recherche Windows Update indisponible.");
                }

                var items = CollectDriverUpdates(updates, usedFallback);
                _progress?.Report(100);
                return new DriverUpdateResult(
                    Succeeded: true,
                    Found: items.Count,
                    Installed: 0,
                    RebootRequired: false,
                    Items: items,
                    Summary: "Recherche des pilotes terminée.",
                    FailureReason: null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (COMException ex)
            {
                Log.Error($"WUA driver scan COM error: 0x{ex.HResult:X8} {ex.Message}");
                return Failure("Erreur Windows Update.");
            }
            catch (Exception ex)
            {
                Log.Error($"WUA driver scan error: {ex.Message}");
                return Failure("Erreur Windows Update.");
            }
        }

        private DriverUpdateResult InstallInternal(IReadOnlyList<DriverUpdateItem> items, CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                _progress?.Report(0);

                if (items is null || items.Count == 0)
                {
                    return Failure("Aucune mise à jour de pilotes à installer.", "Aucune mise à jour de pilotes à installer.");
                }

                var session = CreateSession();
                if (session is null)
                {
                    return Failure("Service Windows Update indisponible.");
                }

                var updates = SearchDriverUpdates(session, out bool usedFallback);
                if (updates is null)
                {
                    return Failure("Recherche Windows Update indisponible.");
                }

                var matching = BuildMatchingCollection(items, updates, usedFallback);
                var found = (int)matching.Count;
                if (found == 0)
                {
                    return Failure("Aucune mise à jour de pilotes à installer.", "Aucune mise à jour de pilotes à installer.");
                }

                ct.ThrowIfCancellationRequested();
                _progress?.Report(40);

                dynamic downloader = session.CreateUpdateDownloader();
                downloader.Updates = matching;
                dynamic downloadResult = downloader.Download();

                if (!IsSuccess(downloadResult.ResultCode))
                {
                    var hresult = TryGetHresult(downloadResult);
                    LogResultFailure("download", hresult, downloadResult);
                    return Failure("Téléchargement des pilotes échoué.", BuildFailureReason(hresult, downloadResult));
                }

                dynamic downloaded = CreateUpdateCollection(matching, onlyDownloaded: true);
                var downloadedCount = (int)downloaded.Count;
                if (downloadedCount == 0)
                {
                    return Failure("Aucune mise à jour de pilotes téléchargée.", "Téléchargement des pilotes échoué.");
                }

                _progress?.Report(70);

                dynamic installer = session.CreateUpdateInstaller();
                installer.Updates = downloaded;
                dynamic installResult = installer.Install();

                var rebootRequired = installResult.RebootRequired is bool flag && flag;
                var installed = CountInstalled(downloaded);

                if (!IsSuccess(installResult.ResultCode))
                {
                    var hresult = TryGetHresult(installResult);
                    LogResultFailure("install", hresult, installResult);
                    return Failure("Installation des pilotes échouée.", BuildFailureReason(hresult, installResult), found, installed, rebootRequired);
                }

                if (installed == 0)
                {
                    return Failure("Aucune mise à jour de pilotes installée.", "Installation des pilotes échouée.", found, 0, rebootRequired);
                }

                _progress?.Report(100);
                return new DriverUpdateResult(true, found, installed, rebootRequired, new List<DriverUpdateItem>(), "Installation des pilotes terminée.", null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (COMException ex)
            {
                Log.Error($"WUA driver install COM error: 0x{ex.HResult:X8} {ex.Message}");
                return Failure("Erreur Windows Update.", BuildFailureReason(ex.HResult, ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"WUA driver install error: {ex.Message}");
                return Failure("Erreur Windows Update.", "Erreur Windows Update.");
            }
        }

        private static dynamic? CreateSession()
        {
            var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
            if (sessionType is null)
            {
                return null;
            }

            return Activator.CreateInstance(sessionType);
        }

        private static dynamic? SearchDriverUpdates(dynamic session, out bool usedFallback)
        {
            usedFallback = false;
            dynamic searcher = session.CreateUpdateSearcher();
            searcher.Online = true;

            try
            {
                dynamic result = searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Driver'");
                return result.Updates;
            }
            catch (COMException ex)
            {
                usedFallback = true;
                Log.Warn($"WUA driver criteria failed, fallback search: 0x{ex.HResult:X8} {ex.Message}");
                dynamic result = searcher.Search("IsInstalled=0 and IsHidden=0");
                return result.Updates;
            }
        }

        private static dynamic BuildMatchingCollection(IReadOnlyList<DriverUpdateItem> items, dynamic updates, bool filterByCategory)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.UpdateId))
                {
                    ids.Add(item.UpdateId);
                }
                else if (!string.IsNullOrWhiteSpace(item.Title))
                {
                    titles.Add(item.Title);
                }
            }

            var collectionType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");
            dynamic collection = updates;
            if (collectionType is not null)
            {
                var created = Activator.CreateInstance(collectionType);
                if (created is not null)
                {
                    collection = created;
                }
            }

            var count = (int)updates.Count;
            for (var i = 0; i < count; i++)
            {
                dynamic update = updates.Item(i);
                if (filterByCategory && !IsDriverCategory(update))
                {
                    continue;
                }

                var updateId = TryGetUpdateId(update);
                var title = update.Title as string;
                if ((updateId is not null && ids.Contains(updateId)) ||
                    (!string.IsNullOrWhiteSpace(title) && titles.Contains(title)))
                {
                    collection.Add(update);
                }
            }

            return collection;
        }

        private static List<DriverUpdateItem> CollectDriverUpdates(dynamic updates, bool filterByCategory)
        {
            var items = new List<DriverUpdateItem>();
            var count = (int)updates.Count;
            for (var i = 0; i < count; i++)
            {
                dynamic update = updates.Item(i);
                if (filterByCategory && !IsDriverCategory(update))
                {
                    continue;
                }

                var title = update.Title as string ?? "Pilote sans nom";
                var updateId = TryGetUpdateId(update);
                items.Add(new DriverUpdateItem(title, updateId));
            }

            return items;
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

        private static bool IsDriverCategory(dynamic update)
        {
            try
            {
                dynamic categories = update.Categories;
                var count = (int)categories.Count;
                for (var i = 0; i < count; i++)
                {
                    dynamic category = categories.Item(i);
                    var name = category.Name as string;
                    if (!string.IsNullOrWhiteSpace(name)
                        && name.IndexOf("driver", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static string? TryGetUpdateId(dynamic update)
        {
            try
            {
                return update.Identity?.UpdateID as string;
            }
            catch
            {
            }

            try
            {
                return update.UpdateID as string;
            }
            catch
            {
            }

            return null;
        }

        private static DriverUpdateResult Failure(string summary, string? failureReason = null)
            => new(false, 0, 0, false, new List<DriverUpdateItem>(), summary, failureReason ?? summary);

        private static DriverUpdateResult Failure(string summary, string? failureReason, int found, int installed, bool rebootRequired)
            => new(false, found, installed, rebootRequired, new List<DriverUpdateItem>(), summary, failureReason ?? summary);

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

        private static string BuildFailureReason(int? hresult, dynamic? result)
        {
            if (IsAccessDenied(hresult, result))
            {
                return "Droits administrateur requis";
            }

            return "Erreur Windows Update.";
        }

        private static void LogResultFailure(string operation, int? hresult, dynamic? result)
        {
            var hresultText = hresult.HasValue ? $"0x{hresult.Value:X8}" : "unknown";
            var details = SafeToString(result);
            Log.Error($"WUA driver {operation} failed: hresult={hresultText}; details={details}");
        }

        private static string SafeToString(dynamic? value)
        {
            try
            {
                return value?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsAccessDenied(int? hresult, dynamic? result)
        {
            if (hresult is int value && value == unchecked((int)0x80070005))
            {
                return true;
            }

            try
            {
                var message = result?.ToString();
                return message is not null && message.Contains("access", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
