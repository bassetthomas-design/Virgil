using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Virgil.App.Services
{
    public sealed class OpenAiKeyStore : ISecretStore
    {
        private static readonly string KeyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Virgil",
            "openai.key");

        public void SaveOpenAiApiKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("API key must be provided.", nameof(key));
            }

            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(KeyPath)!);
            var bytes = Encoding.UTF8.GetBytes(key.Trim());
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyPath, protectedBytes);
        }

        public string? LoadOpenAiApiKey()
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            if (!File.Exists(KeyPath))
            {
                return null;
            }

            try
            {
                var protectedBytes = File.ReadAllBytes(KeyPath);
                var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                var key = Encoding.UTF8.GetString(bytes);
                return string.IsNullOrWhiteSpace(key) ? null : key;
            }
            catch
            {
                return null;
            }
        }

        public void ClearOpenAiApiKey()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            if (File.Exists(KeyPath))
            {
                File.Delete(KeyPath);
            }
        }
    }
}
