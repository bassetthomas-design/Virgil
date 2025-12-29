using System;
using System.Security.Principal;

namespace Virgil.Services.Network;

public interface IPrivilegeChecker
{
    bool IsAdministrator();
}

public sealed class WindowsPrivilegeChecker : IPrivilegeChecker
{
    public bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
