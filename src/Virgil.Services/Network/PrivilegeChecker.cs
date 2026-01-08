using System;
using Virgil.Core;

namespace Virgil.Services.Network;

public interface IPrivilegeChecker
{
    bool IsAdministrator();
}

public sealed class WindowsPrivilegeChecker : IPrivilegeChecker
{
    public bool IsAdministrator()
    {
        return ProcessElevation.IsProcessElevated();
    }
}
