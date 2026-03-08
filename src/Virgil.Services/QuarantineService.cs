using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services;

public sealed class QuarantineService
{
    private readonly string _quarantineRoot;

    public QuarantineService(string? quarantineRoot = null)
    {
        _quarantineRoot = quarantineRoot
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Virgil", "Quarantine");
    }

    public string QuarantineRoot => _quarantineRoot;

    public async Task<string> MoveFileAsync(string sourcePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_quarantineRoot);
        var target = BuildTargetPath(sourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await Task.Run(() => File.Move(sourcePath, target), ct).ConfigureAwait(false);
        return target;
    }

    public async Task<string> MoveFolderAsync(string sourcePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_quarantineRoot);
        var target = BuildTargetPath(sourcePath);
        await Task.Run(() => Directory.Move(sourcePath, target), ct).ConfigureAwait(false);
        return target;
    }

    private string BuildTargetPath(string sourcePath)
    {
        var safeName = sourcePath.Trim(Path.DirectorySeparatorChar).Replace(':', '_');
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(_quarantineRoot, stamp, safeName);
    }
}
