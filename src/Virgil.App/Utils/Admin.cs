using System.Security.Principal;

namespace Virgil.App.Utils
{
    public static class Admin
    {
        public static bool IsElevated()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
