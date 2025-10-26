using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Gestion simple des programmes lancés au démarrage (Run HKCU/HKLM).
    /// </summary>
    public sealed class StartupEntry
    {
        public string Hive { get; set; } = ""; // HKCU ou HKLM
        public string Name { get; set; } = "";
        public string? Command { get; set; }
    }

    public sealed class StartupManager
    {
        private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public IReadOnlyList<StartupEntry> ListAll()
        {
            var list = new List<StartupEntry>();
            ReadHive(Registry.CurrentUser, "HKCU", list);
            try { ReadHive(Registry.LocalMachine, "HKLM", list); } catch { /* besoin admin */ }
            return list;
        }

        public bool AddOrUpdateUser(string name, string command)
        {
            try
            {
                using var rk = Registry.CurrentUser.CreateSubKey(RunPath);
                rk.SetValue(name, command);
                return true;
            }
            catch { return false; }
        }

        public bool RemoveUser(string name)
        {
            try
            {
                using var rk = Registry.CurrentUser.CreateSubKey(RunPath);
                rk.DeleteValue(name, throwOnMissingValue: false);
                return true;
            }
            catch { return false; }
        }

        public bool AddOrUpdateMachine(string name, string command)
        {
            try
            {
                using var rk = Registry.LocalMachine.CreateSubKey(RunPath);
                rk.SetValue(name, command);
                return true;
            }
            catch { return false; } // nécessite admin
        }

        public bool RemoveMachine(string name)
        {
            try
            {
                using var rk = Registry.LocalMachine.CreateSubKey(RunPath);
                rk.DeleteValue(name, throwOnMissingValue: false);
                return true;
            }
            catch { return false; }
        }

        private static void ReadHive(RegistryKey hive, string hiveName, List<StartupEntry> list)
        {
            try
            {
                using var rk = hive.OpenSubKey(RunPath, writable: false);
                if (rk == null) return;
                foreach (var name in rk.GetValueNames())
                {
                    string? cmd = null;
                    try { cmd = rk.GetValue(name)?.ToString(); } catch { }
                    list.Add(new StartupEntry { Hive = hiveName, Name = name, Command = cmd });
                }
            }
            catch { /* ignore */ }
        }
    }
}
