using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;
using NAudio.Wave.Asio;

namespace LazyBootstrap
{
    internal static class AsioDriverRegistry
    {
        private const string AsioRegistryPath = @"SOFTWARE\ASIO";
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, AsioDriver> ControlPanelDrivers = new Dictionary<string, AsioDriver>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<string> GetInstalledDriverNames()
        {
            if (!OperatingSystem.IsWindows())
            {
                return Array.Empty<string>();
            }

            var driverNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            LoadDriverNames(driverNames);

            return driverNames
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static bool TryOpenControlPanel(string driverName, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!OperatingSystem.IsWindows())
            {
                errorMessage = "当前平台不支持 ASIO 控制面板。";
                return false;
            }

            var normalizedDriverName = driverName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedDriverName))
            {
                errorMessage = "请先选择一个 ASIO 驱动。";
                return false;
            }

            try
            {
                var driver = GetOrCreateControlPanelDriver(normalizedDriverName);
                driver.ControlPanel();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static void DisposeControlPanelDrivers()
        {
            lock (SyncRoot)
            {
                foreach (var driver in ControlPanelDrivers.Values)
                {
                    try
                    {
                        driver.ReleaseComAsioDriver();
                    }
                    catch
                    {
                    }
                }

                ControlPanelDrivers.Clear();
            }
        }

        [SupportedOSPlatform("windows")]
        private static void LoadDriverNames(HashSet<string> driverNames)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
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

        [SupportedOSPlatform("windows")]
        private static AsioDriver GetOrCreateControlPanelDriver(string driverName)
        {
            lock (SyncRoot)
            {
                if (ControlPanelDrivers.TryGetValue(driverName, out var existingDriver))
                {
                    return existingDriver;
                }

                var driver = AsioDriver.GetAsioDriverByName(driverName);
                try
                {
                    if (!driver.Init(IntPtr.Zero))
                    {
                        throw new InvalidOperationException(driver.GetErrorMessage());
                    }

                    ControlPanelDrivers[driverName] = driver;
                    return driver;
                }
                catch
                {
                    try
                    {
                        driver.ReleaseComAsioDriver();
                    }
                    catch
                    {
                    }

                    throw;
                }
            }
        }
    }
}
