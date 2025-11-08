namespace Virgil.App.Services;

public interface IGpuService
{
    (double usage, double temp) Read();
}
