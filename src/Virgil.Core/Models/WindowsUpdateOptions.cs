namespace Virgil.Core.Models;

public sealed record WindowsUpdateOptions
{
    public bool IncludeDrivers { get; init; } = true;
    public bool SearchOnly { get; init; }
}
