using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Network;

public sealed class HttpInternetSpeedProbe : IInternetSpeedProbe, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IPingClient _pingClient;
    private readonly IReadOnlyList<SpeedTestEndpoint> _endpoints;

    public HttpInternetSpeedProbe(IPingClient pingClient, HttpMessageHandler? handler = null, IReadOnlyList<SpeedTestEndpoint>? endpoints = null)
    {
        _pingClient = pingClient;
        _httpClient = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _httpClient.Timeout = TimeSpan.FromSeconds(15);

        _endpoints = endpoints ?? new List<SpeedTestEndpoint>
        {
            new(
                "Cloudflare",
                new Uri("https://speed.cloudflare.com/__down?bytes=3000000"),
                new Uri("https://speed.cloudflare.com/__up"),
                DownloadBytes: 3_000_000,
                UploadBytes: 600_000),
            new(
                "Cloudflare (fallback)",
                new Uri("https://speed.cloudflare.com/__down?bytes=1500000"),
                new Uri("https://speed.cloudflare.com/__up"),
                DownloadBytes: 1_500_000,
                UploadBytes: 400_000),
        };
    }

    public async Task<SpeedTestProbeResult> MeasureAsync(CancellationToken ct = default)
    {
        var usedFallback = false;

        foreach (var endpoint in _endpoints)
        {
            try
            {
                var result = await MeasureOnEndpointAsync(endpoint, ct).ConfigureAwait(false);
                if (result.Success)
                {
                    return usedFallback
                        ? result with { UsedFallback = true, ServerLabel = $"{endpoint.Label} (fallback)" }
                        : result;
                }

                usedFallback = true;
            }
            catch (TaskCanceledException)
            {
                return SpeedTestProbeResult.Fail(endpoint.Label, "Serveur de test indisponible (timeout)", timedOut: true);
            }
            catch
            {
                usedFallback = true;
            }
        }

        return SpeedTestProbeResult.Fail("Serveur inconnu", "Impossible de joindre un serveur de test");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<SpeedTestProbeResult> MeasureOnEndpointAsync(SpeedTestEndpoint endpoint, CancellationToken ct)
    {
        var latency = await MeasureLatencyAsync(endpoint.DownloadUri.Host, ct).ConfigureAwait(false);
        var download = await MeasureDownloadAsync(endpoint, ct).ConfigureAwait(false);
        var upload = await MeasureUploadAsync(endpoint, ct).ConfigureAwait(false);

        if (download.speed <= 0 || upload <= 0)
        {
            return SpeedTestProbeResult.Fail(endpoint.Label, "Serveur de test indisponible");
        }

        var stability = download.variation ?? ComputeRelativeGap(download.speed, upload);
        var latencyValue = latency ?? 0;

        return new SpeedTestProbeResult(
            Success: true,
            ServerLabel: endpoint.Label,
            DownloadMbps: download.speed,
            UploadMbps: upload,
            LatencyMs: latencyValue,
            StabilityVariationPercent: stability,
            UsedFallback: false);
    }

    private async Task<double?> MeasureLatencyAsync(string host, CancellationToken ct)
    {
        var samples = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            var attempt = await _pingClient.SendAsync(host, timeoutMs: 1500, ct).ConfigureAwait(false);
            if (attempt.Status == PingAttemptStatus.Success)
            {
                samples.Add(attempt.RoundtripTimeMs);
            }
        }

        if (samples.Count == 0)
        {
            return null;
        }

        return samples.Average();
    }

    private async Task<(double speed, double? variation)> MeasureDownloadAsync(SpeedTestEndpoint endpoint, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(endpoint.DownloadUri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return (0, null);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var buffer = new byte[8192];
        long totalBytes = 0;
        var samples = new List<double>();
        var sw = Stopwatch.StartNew();

        while (totalBytes < endpoint.DownloadBytes && !ct.IsCancellationRequested)
        {
            var toRead = (int)Math.Min(buffer.Length, endpoint.DownloadBytes - totalBytes);
            var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalBytes += read;
            if (sw.ElapsedMilliseconds >= 250 && totalBytes > 0)
            {
                samples.Add(ComputeMbps(totalBytes, sw.Elapsed));
            }
        }

        sw.Stop();
        if (totalBytes == 0 || sw.ElapsedMilliseconds == 0)
        {
            return (0, null);
        }

        var speed = ComputeMbps(totalBytes, sw.Elapsed);

        double? variation = null;
        if (samples.Count > 1)
        {
            var avg = samples.Average();
            var variance = samples.Average(s => Math.Pow(s - avg, 2));
            var stdDev = Math.Sqrt(variance);
            variation = avg > 0 ? stdDev / avg * 100 : null;
        }

        return (speed, variation);
    }

    private async Task<double> MeasureUploadAsync(SpeedTestEndpoint endpoint, CancellationToken ct)
    {
        var payload = new byte[endpoint.UploadBytes];
        new Random().NextBytes(payload);
        using var content = new ByteArrayContent(payload);
        var sw = Stopwatch.StartNew();
        using var response = await _httpClient.PostAsync(endpoint.UploadUri, content, ct).ConfigureAwait(false);
        sw.Stop();

        if (!response.IsSuccessStatusCode || sw.ElapsedMilliseconds == 0)
        {
            return 0;
        }

        return ComputeMbps(payload.Length, sw.Elapsed);
    }

    private static double ComputeMbps(long bytes, TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds <= 0)
        {
            return 0;
        }

        var bits = bytes * 8d;
        var megabits = bits / 1_000_000d;
        return megabits / elapsed.TotalSeconds;
    }

    private static double ComputeRelativeGap(double left, double right)
    {
        var max = Math.Max(left, right);
        var min = Math.Min(left, right);
        if (max <= 0)
        {
            return 0;
        }

        return (max - min) / max * 100;
    }
}

public sealed record SpeedTestEndpoint(string Label, Uri DownloadUri, Uri UploadUri, int DownloadBytes, int UploadBytes);
