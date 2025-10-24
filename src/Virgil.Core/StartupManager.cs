using System.Collections.Generic;
using Microsoft.Win32;

namespace Virgil.Core
{
    /// <summary>
    /// Manages startup programs by reading and modifying the Run keys in the Windows registry.
    /// </summary>
    public class StartupManager
    {
        private static readonly string[] RunKeys =
        {
            @"Software\Microsoft\Windows\CurrentVersion\Run"
        };

        /// <summary>
        /// Lists all startup entries found in the current user Run registry key.
        /// </summary>
        public List<(string Name, string Command)> ListStartupPrograms()
        {
            var list = new List<(string, string)>();
            foreach (var keyPath in RunKeys)
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
                if (key != null)
                {
                    foreach (var name in key.GetValueNames())
                    {
                        var value = key.GetValue(name)?.ToString() ?? string.Empty;
                        list.Add((name, value));
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Adds or updates a startup entry for the given program name and command.
        /// </summary>
        public void EnableStartup(string name, string command)
        {
            foreach (var keyPath in RunKeys)
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
                key?.SetValue(name, command);
            }
        }

        /// <summary>
        /// Removes a startup entry by name.
        /// </summary>
        public void DisableStartup(string name)
        {
            foreach (var keyPath in RunKeys)
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
                key?.DeleteValue(name, throwOnMissingValue: false);
            }
        }
    }
}