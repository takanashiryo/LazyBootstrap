using System;
using System.Collections.Generic;
using System.IO;
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

            if (!File.Exists(configPath))
            {
                config.ReplaceWithText(CreateDefaultConfigText());
            }
            else if (!config.TryValidate(out _))
            {
                config.BackupInvalidAndReplace(CreateDefaultConfigText());
            }

            EnsureDefaults(config);
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
