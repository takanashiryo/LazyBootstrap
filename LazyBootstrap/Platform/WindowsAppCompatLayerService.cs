using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace LazyBootstrap.Platform
{
    public sealed class WindowsAppCompatLayerService
    {
        private const string AppCompatLayersRegistryPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        private const string FsoDisabledToken = "DISABLEDXMAXIMIZEDWINDOWEDMODE";
        private const string LayerPrefix = "~";

        public bool IsFsoDisabled(string executablePath)
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            string normalizedPath = NormalizeExecutablePath(executablePath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return false;
            }

            try
            {
                return IsFsoDisabledWindows(normalizedPath);
            }
            catch
            {
                return false;
            }
        }

        public bool TrySetFsoDisabled(string executablePath, bool disabled, out string error)
        {
            error = string.Empty;

            if (!OperatingSystem.IsWindows())
            {
                error = "当前平台不支持 Windows 兼容性设置。";
                return false;
            }

            string normalizedPath = NormalizeExecutablePath(executablePath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                error = "spice64.exe 路径无效。";
                return false;
            }

            try
            {
                SetFsoDisabledWindows(normalizedPath, disabled);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string NormalizeExecutablePath(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(executablePath.Trim());
            }
            catch
            {
                return executablePath.Trim();
            }
        }

        [SupportedOSPlatform("windows")]
        private static bool IsFsoDisabledWindows(string executablePath)
        {
            using var layersKey = Registry.CurrentUser.OpenSubKey(AppCompatLayersRegistryPath, writable: false);
            string value = layersKey?.GetValue(executablePath) as string ?? string.Empty;
            return ParseLayerTokens(value).Contains(FsoDisabledToken);
        }

        [SupportedOSPlatform("windows")]
        private static void SetFsoDisabledWindows(string executablePath, bool disabled)
        {
            using var layersKey = Registry.CurrentUser.CreateSubKey(AppCompatLayersRegistryPath, writable: true);
            if (layersKey == null)
            {
                throw new InvalidOperationException("无法打开 Windows 兼容性注册表项。");
            }

            string currentValue = layersKey.GetValue(executablePath) as string ?? string.Empty;
            var tokens = ParseLayerTokens(currentValue);

            if (disabled)
            {
                tokens.Add(FsoDisabledToken);
            }
            else
            {
                tokens.Remove(FsoDisabledToken);
            }

            if (tokens.Count == 0)
            {
                layersKey.DeleteValue(executablePath, throwOnMissingValue: false);
                return;
            }

            layersKey.SetValue(executablePath, BuildLayerValue(tokens), RegistryValueKind.String);
        }

        private static HashSet<string> ParseLayerTokens(string value)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in (value ?? string.Empty)
                         .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                         .Select(token => token.Trim())
                         .Where(token => !string.IsNullOrWhiteSpace(token)))
            {
                if (string.Equals(token, LayerPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                tokens.Add(token);
            }

            return tokens;
        }

        private static string BuildLayerValue(HashSet<string> tokens)
        {
            return string.Join(
                " ",
                tokens
                    .Prepend(LayerPrefix)
                    .Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }
}
