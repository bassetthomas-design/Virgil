namespace Virgil.Core.Config;

public sealed class Thresholds
{
    public int CpuWarn  { get; set; } = 75;
    public int CpuAlert { get; set; } = 90;
    public int GpuWarn  { get; set; } = 75;
    public int GpuAlert { get; set; } = 90;
    public int MemWarn  { get; set; } = 80;
    public int MemAlert { get; set; } = 95;
    public int DiskWarn { get; set; } = 80;
    public int DiskAlert{ get; set; } = 95;
}

public interface IConfigService
{
    Thresholds GetThresholds();
}

public sealed class ConfigService : IConfigService
{
    private readonly Thresholds _t = new();
    public Thresholds GetThresholds() => _t;
}
