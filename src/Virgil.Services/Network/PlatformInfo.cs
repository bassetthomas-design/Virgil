using System;

namespace Virgil.Services.Network;

public interface IPlatformInfo
{
    bool IsWindows();
}

public sealed class RuntimePlatformInfo : IPlatformInfo
{
    public bool IsWindows() => OperatingSystem.IsWindows();
}
