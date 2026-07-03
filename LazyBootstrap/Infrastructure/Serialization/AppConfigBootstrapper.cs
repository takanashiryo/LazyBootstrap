using System;
using System.Collections.Generic;
using System.Linq;

namespace LazyBootstrap.Infrastructure.Serialization
{
    internal static class AppConfigBootstrapper
    {
        public const string SettingSectionName = "Setting";
        public const string DisplaySectionName = "Display";

        private const string ServerSectionName = "Server";
        private const string AsphyxiaPresetName = "Asphyxia";
        private const string AsphyxiaDefaultUrl = "http://localhost:8083";

        private static readonly ConfigDefaultEntry[] Defaults =
        [
            new ConfigDefaultEntry(SettingSectionName, "noasphyxia", "false"),
            new ConfigDefaultEntry(SettingSectionName, "disable-fso", "false"),
            new ConfigDefaultEntry(SettingSectionName, "compatlayer", "false"),
            new ConfigDefaultEntry(SettingSectionName, "cl-rendermode", "dx9on12"),
            new ConfigDefaultEntry(SettingSectionName, "use-system-config", "false"),

            new ConfigDefaultEntry(DisplaySectionName, "displayconfigure", "false"),
            new ConfigDefaultEntry(DisplaySectionName, "exitrestore", "true"),
            new ConfigDefaultEntry(DisplaySectionName, "mode", "single"),
            new ConfigDefaultEntry(DisplaySectionName, "mainscreen", "0"),
            new ConfigDefaultEntry(DisplaySectionName, "subscreen", "0"),
            new ConfigDefaultEntry(DisplaySectionName, "subrotation", "0"),
            new ConfigDefaultEntry(DisplaySectionName, "mainrotation", "0"),
            new ConfigDefaultEntry(DisplaySectionName, "mainresolution", "640x480"),
            new ConfigDefaultEntry(DisplaySectionName, "subresolution", "640x480"),
            new ConfigDefaultEntry(DisplaySectionName, "mainrefresh", "59"),
            new ConfigDefaultEntry(DisplaySectionName, "subrefresh", "59")
        ];

        public static void InitializeAndMigrate(string configPath, ConfigHandler config)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
            ArgumentNullException.ThrowIfNull(config);

            string defaultConfigText = CreateDefaultConfigText();
            ConfigFileHealth health;
            try
            {
                health = config.CheckStartupHealth();
            }
            catch (Exception ex)
            {
                EnterReadOnlySession(config, defaultConfigText, $"读取 config.toml 失败：{ex.Message}");
                EnsureDefaults(config);
                return;
            }

            if (health.Status == ConfigFileHealthStatus.Inaccessible)
            {
                string seedText = string.IsNullOrWhiteSpace(health.Content)
                    ? defaultConfigText
                    : health.Content;
                EnterReadOnlySession(config, seedText, $"config.toml 无法读取或保存：{health.ErrorMessage}");
                EnsureDefaults(config);
                return;
            }

            if (health.Status == ConfigFileHealthStatus.Missing)
            {
                if (!TryRunStartupConfigOperation(
                        () => config.ReplaceWithText(defaultConfigText),
                        out var createError))
                {
                    EnterReadOnlySession(config, defaultConfigText, $"创建 config.toml 失败：{createError}");
                    EnsureDefaults(config);
                    return;
                }
            }
            else if (health.Status == ConfigFileHealthStatus.InvalidToml)
            {
                if (!TryRunStartupConfigOperation(
                        () => config.BackupInvalidAndReplace(defaultConfigText),
                        out var repairError))
                {
                    EnterReadOnlySession(config, defaultConfigText, $"config.toml 损坏且无法重建：{repairError}");
                    EnsureDefaults(config);
                    return;
                }
            }

            if (!TryRunStartupConfigOperation(() => EnsureDefaults(config), out var defaultsError))
            {
                string readOnlySeed = health.Status == ConfigFileHealthStatus.Valid
                    ? health.Content
                    : defaultConfigText;
                EnterReadOnlySession(config, readOnlySeed, $"config.toml 无法保存设置：{defaultsError}");
                EnsureDefaults(config);
            }
        }

        private static bool TryRunStartupConfigOperation(Action operation, out string error)
        {
            error = string.Empty;
            try
            {
                operation();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void EnterReadOnlySession(ConfigHandler config, string seedText, string reason)
        {
            config.EnterReadOnlySession(seedText, reason);
        }

        private static void EnsureDefaults(ConfigHandler config)
        {
            foreach (var entry in Defaults)
            {
                EnsureDefault(config, entry.Section, entry.Key, entry.Value);
            }
        }

        private static void EnsureDefault(ConfigHandler config, string section, string key, string defaultValue)
        {
            var existing = config.ReadString(section, key, string.Empty);
            if (string.IsNullOrWhiteSpace(existing))
            {
                config.WriteString(section, key, defaultValue);
            }
        }

        internal static string CreateDefaultConfigText()
        {
            var lines = new List<string>();
            AppendDefaultSection(lines, SettingSectionName);
            lines.Add(string.Empty);
            AppendDefaultSection(lines, DisplaySectionName);
            lines.Add(string.Empty);
            lines.Add($"[{ServerSectionName}]");
            lines.Add(TomlTextShared.BuildStringLine("activepreset", AsphyxiaPresetName));
            lines.Add(string.Empty);
            lines.Add("[[Server.Presets]]");
            lines.Add(TomlTextShared.BuildStringLine("name", AsphyxiaPresetName));
            lines.Add(TomlTextShared.BuildStringLine("serverurl", AsphyxiaDefaultUrl));
            lines.Add(TomlTextShared.BuildStringLine("pcbid", string.Empty));
            return string.Join(Environment.NewLine, lines);
        }

        private static void AppendDefaultSection(List<string> lines, string sectionName)
        {
            lines.Add($"[{sectionName}]");
            foreach (var entry in Defaults.Where(item => string.Equals(item.Section, sectionName, StringComparison.Ordinal)))
            {
                lines.Add(TomlTextShared.BuildStringLine(entry.Key, entry.Value));
            }
        }

        private readonly record struct ConfigDefaultEntry(string Section, string Key, string Value);
    }
}
