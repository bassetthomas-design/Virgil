using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Config;

namespace Virgil.Services.ModelPacks;

public sealed record ModelPackDownloadProgress(
    double? Percent,
    string SpeedText,
    string StatusText,
    bool IsIndeterminate);

public sealed record ModelPackDownloadResult(
    bool Success,
    string StatusText,
    string? ErrorMessage = null);

public sealed record ModelPackVerificationResult(
    bool IsValid,
    string StatusText,
    string? ErrorMessage = null);

public sealed class ModelPackDownloader
{
    private readonly ModelLocator _modelLocator;
    private readonly HttpClient _httpClient;
    private string? _lastTempPath;

    public ModelPackDownloader(ModelLocator modelLocator, HttpClient? httpClient = null)
    {
        _modelLocator = modelLocator ?? throw new ArgumentNullException(nameof(modelLocator));
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<ModelPackDownloadResult> DownloadAsync(
        ModelPackManifest manifest,
        IProgress<ModelPackDownloadProgress>? progress,
        CancellationToken ct)
    {
        if (manifest is null)
        {
            throw new ArgumentNullException(nameof(manifest));
        }

        progress?.Report(new ModelPackDownloadProgress(0, "—", "Téléchargement…", false));

        Directory.CreateDirectory(_modelLocator.ModelDirectory);
        _lastTempPath = Path.Combine(_modelLocator.ModelDirectory, $"{ModelLocator.ExpectedFileName}.tmp");

        try
        {
            using var response = await _httpClient.GetAsync(
                manifest.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                ct).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;
            var isIndeterminate = totalBytes is null or <= 0;
            progress?.Report(new ModelPackDownloadProgress(0, "—", "Téléchargement…", isIndeterminate));

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var fileStream = new FileStream(
                _lastTempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                8192,
                useAsync: true);

            var buffer = new byte[8192];
            long totalRead = 0;
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                totalRead += read;

                var percent = totalBytes.HasValue && totalBytes.Value > 0
                    ? totalRead / (double)totalBytes.Value * 100
                    : (double?)null;
                var speedText = FormatSpeed(totalRead, stopwatch.Elapsed);
                progress?.Report(new ModelPackDownloadProgress(percent, speedText, "Téléchargement…", isIndeterminate));
            }

            fileStream.Close();

            if (File.Exists(_modelLocator.ModelPath))
            {
                File.Delete(_modelLocator.ModelPath);
            }

            File.Move(_lastTempPath, _modelLocator.ModelPath);
            _lastTempPath = null;

            var hash = await ComputeSha256Async(_modelLocator.ModelPath, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(_modelLocator.ModelHashPath, hash, ct).ConfigureAwait(false);

            progress?.Report(new ModelPackDownloadProgress(100, "—", "Terminé.", false));
            return new ModelPackDownloadResult(true, "Terminé.");
        }
        catch (OperationCanceledException)
        {
            return new ModelPackDownloadResult(false, "Téléchargement annulé.", "Téléchargement annulé.");
        }
        catch (Exception ex)
        {
            return new ModelPackDownloadResult(false, "Échec du téléchargement.", ex.Message);
        }
    }

    public async Task<ModelPackVerificationResult> VerifyAsync()
    {
        if (!_modelLocator.IsInstalled)
        {
            return new ModelPackVerificationResult(false, "Pack Full non installé.");
        }

        if (!File.Exists(_modelLocator.ModelHashPath))
        {
            return new ModelPackVerificationResult(false, "Hash attendu manquant.", "Hash attendu manquant.");
        }

        var expected = (await File.ReadAllTextAsync(_modelLocator.ModelHashPath).ConfigureAwait(false)).Trim();
        var actual = await ComputeSha256Async(_modelLocator.ModelPath, CancellationToken.None).ConfigureAwait(false);
        if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            return new ModelPackVerificationResult(true, "Vérification OK.");
        }

        return new ModelPackVerificationResult(false, "Hash incorrect.", "Pack Full corrompu: hash incorrect.");
    }

    public void CleanupTemporaryFiles()
    {
        if (_lastTempPath is null || !File.Exists(_lastTempPath))
        {
            return;
        }

        try
        {
            File.Delete(_lastTempPath);
        }
        catch
        {
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static string FormatSpeed(long bytes, TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds <= 0.5)
        {
            return "—";
        }

        var bytesPerSecond = bytes / elapsed.TotalSeconds;
        return $"{FormatBytes(bytesPerSecond)}/s";
    }

    private static string FormatBytes(double bytes)
    {
        string[] suffixes = { "o", "Ko", "Mo", "Go" };
        var order = 0;
        while (bytes >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            bytes /= 1024;
        }

        return $"{bytes:0.0} {suffixes[order]}";
    }
}
