using System;
using System.Collections.Generic;
using System.IO;

namespace LazyBootstrap
{
    internal static class AppConfigBootstrapper
    {
        public const string SettingSectionName = "Setting";
        public const string LegacySettingsSectionName = "Settings";
        public const string DisplaySectionName = "Display";

        public static void InitializeAndMigrate(string configPath, ConfigHandler config)
        {
            if (!File.Exists(configPath))
            {
                WriteInitialConfigToml(configPath);
            }

            config.RenameSection(LegacySettingsSectionName, SettingSectionName);
            config.MoveKey(SettingSectionName, DisplaySectionName, "displayconfigure");
            config.MoveKey(SettingSectionName, DisplaySectionName, "norestorerotation");

            // Migrate old usepreconfig key to portablemode
            var oldValue = config.ReadString(SettingSectionName, "usepreconfig", string.Empty);
            if (!string.IsNullOrWhiteSpace(oldValue))
            {
                config.WriteString(SettingSectionName, "portablemode", oldValue);
            }

            EnsureDefaults(config);
        }

        private static void EnsureDefaults(ConfigHandler config)
        {
            EnsureDefault(config, SettingSectionName, "portablemode", "false");
            EnsureDefault(config, SettingSectionName, "noasphyxia", "false");
            EnsureDefault(config, SettingSectionName, "compatlayerenabled", "false");
            EnsureDefault(config, SettingSectionName, "rendermode", "dx9on12");

            EnsureDefault(config, DisplaySectionName, "displayconfigure", "false");
            EnsureDefault(config, DisplaySectionName, "norestorerotation", "false");
            EnsureDefault(config, DisplaySectionName, "mode", "dual");
            EnsureDefault(config, DisplaySectionName, "mainscreen", "0");
            EnsureDefault(config, DisplaySectionName, "subscreen", "0");
            EnsureDefault(config, DisplaySectionName, "subrotation", "0");
            EnsureDefault(config, DisplaySectionName, "mainrotation", "0");
            EnsureDefault(config, DisplaySectionName, "mainresolution", "640x480");
            EnsureDefault(config, DisplaySectionName, "subresolution", "640x480");
            EnsureDefault(config, DisplaySectionName, "mainrefresh", "59");
            EnsureDefault(config, DisplaySectionName, "subrefresh", "59");
        }

        private static void EnsureDefault(ConfigHandler config, string section, string key, string defaultValue)
        {
            var existing = config.ReadString(section, key, string.Empty);
            if (string.IsNullOrWhiteSpace(existing))
            {
                config.WriteString(section, key, defaultValue);
            }
        }

        private static void WriteInitialConfigToml(string configPath)
        {
            var dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var lines = new List<string>
            {
                "[Setting]",
                "portablemode = \"false\"",
                "noasphyxia = \"false\"",
                "compatlayerenabled = \"false\"",
                "rendermode = \"dx9on12\"",
                string.Empty,
                "[Display]",
                "displayconfigure = \"false\"",
                "norestorerotation = \"false\"",
                "mode = \"dual\"",
                "mainscreen = \"0\"",
                "subscreen = \"0\"",
                "subrotation = \"0\"",
                "mainrotation = \"0\"",
                "mainresolution = \"640x480\"",
                "subresolution = \"640x480\"",
                "mainrefresh = \"59\"",
                "subrefresh = \"59\"",
                string.Empty,
                "[Server]",
                "activepreset = \"Asphyxia\"",
                string.Empty,
                "[[Server.Presets]]",
                "name = \"Asphyxia\"",
                "serverurl = \"http://localhost:8083\"",
                "pcbid = \"\""
            };

            File.WriteAllText(configPath, string.Join(Environment.NewLine, lines), TomlTextShared.Utf8NoBom);
        }
    }
}
