using System;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Virgil.Core;

public static class ProcessElevation
{
    public static bool IsProcessElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (identity is null)
            {
                return false;
            }

            var principal = new WindowsPrincipal(identity);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                return true;
            }

            return IsTokenElevated(identity);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTokenElevated(WindowsIdentity identity)
    {
        try
        {
            var token = identity.AccessToken;
            if (token.IsInvalid || token.IsClosed)
            {
                return false;
            }

            var size = Marshal.SizeOf<TokenElevation>();
            if (!GetTokenInformation(token.DangerousGetHandle(), TokenInformationClass.TokenElevation, out var elevation, size, out _))
            {
                return false;
            }

            return elevation.TokenIsElevated != 0;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        TokenInformationClass tokenInformationClass,
        out TokenElevation tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    private enum TokenInformationClass
    {
        TokenElevation = 20
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevation
    {
        public int TokenIsElevated;
    }
}
