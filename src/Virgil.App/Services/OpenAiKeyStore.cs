using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Virgil.App.Services
{
    public sealed class OpenAiKeyStore
    {
        private static readonly string KeyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Virgil",
            "openai.key");

        public void Save(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key must be provided.", nameof(apiKey));
            }

            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(KeyPath)!);
            var bytes = Encoding.UTF8.GetBytes(apiKey.Trim());
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyPath, protectedBytes);
        }

        public string? Load()
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

        public void Clear()
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
