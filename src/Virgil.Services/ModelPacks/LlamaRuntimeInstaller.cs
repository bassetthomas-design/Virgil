using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Logging;

namespace Virgil.Services.ModelPacks;

public sealed record LlamaRuntimeInstallResult(
    bool Success,
    string StatusText,
    string? ErrorMessage = null,
    string? Diagnostics = null);

public sealed class LlamaRuntimeInstaller
{
    private const string RuntimeRoot = @"D:\\Virgil\\AI\\Runtime";
    private const string RuntimeExecutableName = "llama-server.exe";
    private const string RuntimeVersionFileName = "llama-runtime.version.txt";
    private const string ModelPath = @"D:\\Virgil\\AI\\Models\\Meta-Llama-3.1-8B-Instruct-Q5_K_M.gguf";
    private const string GitHubReleaseApi = "https://api.github.com/repos/ggml-org/llama.cpp/releases/latest";
    private static readonly TimeSpan RuntimeProbeTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RuntimeProbeDelay = TimeSpan.FromSeconds(2);

    private readonly HttpClient _httpClient;

    public LlamaRuntimeInstaller(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Virgil-PackFull-Installer");
        }
    }

    public async Task<LlamaRuntimeInstallResult> InstallAndVerifyAsync(
        IProgress<string>? progress,
        CancellationToken ct)
    {
        progress?.Report("Téléchargement du runtime officiel…");

        try
        {
            var release = await GetLatestReleaseAsync(ct).ConfigureAwait(false);
            var tempRoot = Path.Combine(Path.GetTempPath(), $"llama-runtime-{Guid.NewGuid():N}");
            var zipPath = Path.Combine(tempRoot, "llama-runtime.zip");
            var extractPath = Path.Combine(tempRoot, "extract");

            Directory.CreateDirectory(tempRoot);

            try
            {
                progress?.Report($"Téléchargement runtime {release.Tag}…");
                await DownloadAsync(_httpClient, release.AssetUrl, zipPath, ct).ConfigureAwait(false);

                var sha256 = await ComputeSha256Async(zipPath, ct).ConfigureAwait(false);
                progress?.Report("Extraction du runtime…");

                ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

                var runtimeExe = Directory.EnumerateFiles(extractPath, RuntimeExecutableName, SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(info => info.Length)
                    .FirstOrDefault();
                if (runtimeExe is null)
                {
                    return new LlamaRuntimeInstallResult(false, "Runtime introuvable.", "llama-server.exe non trouvé.");
                }

                var runtimeDir = RuntimeRoot;
                Directory.CreateDirectory(runtimeDir);

                var destinationExe = Path.Combine(runtimeDir, RuntimeExecutableName);
                if (File.Exists(destinationExe))
                {
                    File.Delete(destinationExe);
                }

                foreach (var dll in Directory.EnumerateFiles(runtimeDir, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(dll);
                }

                File.Copy(runtimeExe.FullName, destinationExe, overwrite: true);

                var sourceDlls = Directory.EnumerateFiles(runtimeExe.DirectoryName ?? extractPath, "*.dll", SearchOption.TopDirectoryOnly);
                foreach (var dll in sourceDlls)
                {
                    var destinationDll = Path.Combine(runtimeDir, Path.GetFileName(dll));
                    File.Copy(dll, destinationDll, overwrite: true);
                }

                var versionPath = Path.Combine(runtimeDir, RuntimeVersionFileName);
                var versionContents = $"{release.Tag}{Environment.NewLine}sha256: {sha256}";
                await File.WriteAllTextAsync(versionPath, versionContents, ct).ConfigureAwait(false);

                progress?.Report("Validation runtime…");
                var verifyResult = await VerifyRuntimeAsync(destinationExe, ct).ConfigureAwait(false);
                if (!verifyResult.Success)
                {
                    return verifyResult;
                }

                return new LlamaRuntimeInstallResult(true, "Runtime OK.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    try
                    {
                        Directory.Delete(tempRoot, recursive: true);
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new LlamaRuntimeInstallResult(false, "Installation runtime échouée.", ex.Message, ex.ToString());
        }
    }

    private async Task<LlamaRuntimeInstallResult> VerifyRuntimeAsync(string runtimePath, CancellationToken ct)
    {
        if (!File.Exists(ModelPath))
        {
            var message = $"Modèle introuvable: {ModelPath}";
            return new LlamaRuntimeInstallResult(false, "Test runtime impossible.", message, message);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = runtimePath,
            Arguments = $"--model \"{ModelPath}\" --host 127.0.0.1 --port 8080",
            WorkingDirectory = Path.GetDirectoryName(runtimePath) ?? RuntimeRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return new LlamaRuntimeInstallResult(false, "Échec du démarrage runtime.", "Impossible de lancer llama-server.exe.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        string? lastError = null;
        HttpStatusCode? lastStatus = null;
        string? lastResponse = null;

        try
        {
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://127.0.0.1:8080"),
                Timeout = TimeSpan.FromSeconds(5)
            };

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < RuntimeProbeTimeout && !ct.IsCancellationRequested)
            {
                if (process.HasExited)
                {
                    lastError = "Runtime arrêté pendant la vérification.";
                    break;
                }

                try
                {
                    using var response = await httpClient.GetAsync("/v1/models", ct).ConfigureAwait(false);
                    lastStatus = response.StatusCode;
                    lastResponse = await ReadResponseExcerptAsync(response, ct).ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return new LlamaRuntimeInstallResult(true, "Runtime OK.");
                    }

                    if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
                    {
                        lastError = $"HTTP {(int)response.StatusCode} sur /v1/models";
                        break;
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastError = ex.GetBaseException().Message;
                }
                catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
                {
                    lastError = ex.GetBaseException().Message;
                }

                await Task.Delay(RuntimeProbeDelay, ct).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(lastError) && sw.Elapsed >= RuntimeProbeTimeout)
            {
                lastError = "Timeout pendant la vérification du runtime (60s).";
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }
                catch
                {
                }
            }
        }

        var stdout = await SafeReadAsync(stdoutTask).ConfigureAwait(false);
        var stderr = await SafeReadAsync(stderrTask).ConfigureAwait(false);
        var diagnostics = BuildDiagnostics(runtimePath, lastStatus, lastResponse, lastError, process, stdout, stderr);
        Log.Warn(diagnostics);

        return new LlamaRuntimeInstallResult(
            false,
            "Runtime non compatible.",
            "Le test /v1/models a échoué.",
            diagnostics);
    }

    private static async Task<string> SafeReadAsync(Task<string> task)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildDiagnostics(
        string runtimePath,
        HttpStatusCode? status,
        string? response,
        string? lastError,
        Process process,
        string stdout,
        string stderr)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Diagnostic runtime: échec du test /v1/models.");
        sb.AppendLine($"Executable: {runtimePath}");
        sb.AppendLine($"Model: {ModelPath}");
        sb.AppendLine("Endpoint: http://127.0.0.1:8080/v1/models");

        if (status.HasValue)
        {
            sb.AppendLine($"Last HTTP: {(int)status} {status}");
        }

        if (!string.IsNullOrWhiteSpace(response))
        {
            sb.AppendLine($"Response excerpt: {Truncate(response, 400)}");
        }

        if (!string.IsNullOrWhiteSpace(lastError))
        {
            sb.AppendLine($"Last error: {lastError}");
        }

        if (process.HasExited)
        {
            sb.AppendLine($"Exit code: {process.ExitCode}");
        }

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            sb.AppendLine($"STDOUT: {Truncate(stdout, 400)}");
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            sb.AppendLine($"STDERR: {Truncate(stderr, 400)}");
        }

        return sb.ToString();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value.Trim();
        }

        return value.Substring(0, maxLength).Trim() + "…";
    }

    private static async Task<string?> ReadResponseExcerptAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content is null)
        {
            return null;
        }

        try
        {
            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static async Task DownloadAsync(HttpClient httpClient, string url, string destinationPath, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
        await stream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task<ReleaseAssetInfo> GetLatestReleaseAsync(CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(GitHubReleaseApi, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("tag_name", out var tagElement))
        {
            throw new InvalidOperationException("Réponse GitHub invalide: tag_name manquant.");
        }

        var tag = tagElement.GetString() ?? throw new InvalidOperationException("Réponse GitHub invalide: tag_name vide.");

        if (!document.RootElement.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Réponse GitHub invalide: assets manquants.");
        }

        foreach (var asset in assetsElement.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var nameElement)
                || !asset.TryGetProperty("browser_download_url", out var urlElement))
            {
                continue;
            }

            var name = nameElement.GetString();
            var url = urlElement.GetString();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (name.EndsWith("-bin-win-cpu-x64.zip", StringComparison.OrdinalIgnoreCase))
            {
                return new ReleaseAssetInfo(tag, name, url);
            }
        }

        throw new InvalidOperationException("Aucune release Windows CPU x64 trouvée sur GitHub.");
    }

    private sealed record ReleaseAssetInfo(string Tag, string Name, string AssetUrl);
}
