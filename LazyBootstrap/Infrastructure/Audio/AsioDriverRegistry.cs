using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace LazyBootstrap
{
    internal static class AsioDriverRegistry
    {
        private const string AsioRegistryPath = @"SOFTWARE\ASIO";

        public static IReadOnlyList<string> GetInstalledDriverNames()
        {
            if (!OperatingSystem.IsWindows())
            {
                return Array.Empty<string>();
            }

            var driverNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            LoadDriverNames(driverNames, RegistryView.Registry64);
            LoadDriverNames(driverNames, RegistryView.Registry32);

            return driverNames
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        [SupportedOSPlatform("windows")]
        private static void LoadDriverNames(HashSet<string> driverNames, RegistryView registryView)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
                using var asioKey = baseKey.OpenSubKey(AsioRegistryPath);
                if (asioKey == null)
                {
                    return;
                }

                foreach (var subKeyName in asioKey.GetSubKeyNames())
                {
                    if (!string.IsNullOrWhiteSpace(subKeyName))
                    {
                        driverNames.Add(subKeyName.Trim());
                    }
                }
            }
            catch
            {
            }
        }
    }
}
