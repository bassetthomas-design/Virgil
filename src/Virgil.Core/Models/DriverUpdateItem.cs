namespace Virgil.Core.Models;

public sealed record DriverUpdateItem(
    string Title,
    string? DriverClass,
    string? Manufacturer,
    long Size,
    string? Identity);
