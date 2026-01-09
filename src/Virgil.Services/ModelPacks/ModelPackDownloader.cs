using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Config;
using Virgil.Core.Models;

namespace Virgil.Services.ModelPacks;

public sealed class ModelPackDownloader : IDisposable
{
    private const int BufferSize = 1024 * 128;
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
    private readonly HttpClient _httpClient;

    public ModelPackDownloader(HttpClient? httpClient = null, TimeSpan? timeout = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = timeout ?? TimeSpan.FromMinutes(30);
    }

    public async Task<DownloadResult> DownloadFullPackAsync(
        ModelPackManifest manifest,
        IProgress<DownloadProgress> progress,
        CancellationToken ct)
    {
        if (manifest is null)
        {
            throw new ArgumentNullException(nameof(manifest));
        }

        var targetDirectory = Path.Combine(AppPaths.ProgramDataRoot, "AI", "Models");
        Directory.CreateDirectory(targetDirectory);

        var finalPath = Path.Combine(targetDirectory, manifest.FileName);
        var tempPath = finalPath + ".part";
        DownloadResult? lastFailure = null;

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await DownloadOnceAsync(manifest, tempPath, finalPath, progress, ct).ConfigureAwait(false);
                if (result.Status == DownloadStatus.Failed && attempt < MaxRetries && !ct.IsCancellationRequested)
                {
                    lastFailure = result;
                    await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
                    continue;
                }

                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return DownloadResult.Canceled();
            }
            catch (Exception ex)
            {
                lastFailure = DownloadResult.Failed($"Téléchargement échoué: {ex.Message}");
                if (attempt < MaxRetries)
                {
                    await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
                    continue;
                }

                return lastFailure;
            }
        }

        return lastFailure ?? DownloadResult.Failed("Téléchargement échoué.");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<DownloadResult> DownloadOnceAsync(
        ModelPackManifest manifest,
        string tempPath,
        string finalPath,
        IProgress<DownloadProgress> progress,
        CancellationToken ct)
    {
        var existingBytes = GetExistingLength(tempPath);

        using var request = new HttpRequestMessage(HttpMethod.Get, manifest.DownloadUri);
        if (existingBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingBytes, null);
        }

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && existingBytes > 0)
        {
            return await FinalizeDownloadAsync(tempPath, finalPath, manifest, ct).ConfigureAwait(false);
        }

        if (!response.IsSuccessStatusCode)
        {
            var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase) ? "" : $" {response.ReasonPhrase}";
            return DownloadResult.Failed($"Téléchargement échoué: serveur a répondu {(int)response.StatusCode}{reason}");
        }

        var isPartial = response.StatusCode == HttpStatusCode.PartialContent;
        if (!isPartial && existingBytes > 0)
        {
            existingBytes = 0;
        }

        var contentLength = response.Content.Headers.ContentLength;
        long? totalBytes = contentLength;
        if (isPartial && contentLength.HasValue)
        {
            totalBytes = existingBytes + contentLength.Value;
        }

        var fileMode = existingBytes > 0 ? FileMode.Append : FileMode.Create;
        await using var fileStream = new FileStream(tempPath, fileMode, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        var buffer = new byte[BufferSize];
        var stopwatch = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;
        var downloaded = existingBytes;

        ReportProgress(progress, downloaded, totalBytes, stopwatch);

        while (true)
        {
            var read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            downloaded += read;

            if (stopwatch.Elapsed - lastReport >= TimeSpan.FromMilliseconds(250))
            {
                lastReport = stopwatch.Elapsed;
                ReportProgress(progress, downloaded, totalBytes, stopwatch);
            }
        }

        await fileStream.FlushAsync(ct).ConfigureAwait(false);
        stopwatch.Stop();
        ReportProgress(progress, downloaded, totalBytes, stopwatch);

        if (totalBytes.HasValue && downloaded < totalBytes.Value)
        {
            return DownloadResult.Failed("Téléchargement interrompu avant la fin.");
        }

        return await FinalizeDownloadAsync(tempPath, finalPath, manifest, ct).ConfigureAwait(false);
    }

    private static long GetExistingLength(string path)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        var info = new FileInfo(path);
        return info.Length;
    }

    private static void ReportProgress(IProgress<DownloadProgress> progress, long downloaded, long? total, Stopwatch stopwatch)
    {
        var speed = stopwatch.Elapsed.TotalSeconds > 0 ? downloaded / stopwatch.Elapsed.TotalSeconds : 0;
        double? percent = null;
        if (total.HasValue && total.Value > 0)
        {
            percent = downloaded * 100d / total.Value;
        }

        progress.Report(new DownloadProgress(downloaded, total, percent, speed));
    }

    private static async Task<DownloadResult> FinalizeDownloadAsync(
        string tempPath,
        string finalPath,
        ModelPackManifest manifest,
        CancellationToken ct)
    {
        if (!File.Exists(tempPath))
        {
            return DownloadResult.Failed("Fichier temporaire introuvable.");
        }

        File.Move(tempPath, finalPath, overwrite: true);
        var hash = await ComputeSha256Async(finalPath, ct).ConfigureAwait(false);

        if (!string.Equals(hash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(finalPath);
            return DownloadResult.Failed("Intégrité invalide (SHA256)");
        }

        return DownloadResult.Succeeded(finalPath);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
