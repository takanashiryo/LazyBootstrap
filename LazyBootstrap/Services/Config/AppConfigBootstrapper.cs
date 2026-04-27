using System;
using SystemEnvironment = System.Environment;
using System.Collections.Generic;
using System.IO;

namespace LazyBootstrap.Services.Config
{
    internal static class AppConfigBootstrapper
    {
        public const string SettingSectionName = "Setting";
        public const string LegacySettingsSectionName = "Settings";
        public const string DisplaySectionName = "Display";

        public static void InitializeAndMigrate(string configPath, IConfigHandler config)
        {
            if (!File.Exists(configPath))
            {
                WriteInitialConfigToml(configPath);
            }

            config.RenameSection(LegacySettingsSectionName, SettingSectionName);
            config.MoveKey(SettingSectionName, DisplaySectionName, "displayconfigure");

            var legacyCompatLayer = config.ReadString(SettingSectionName, "compatlayerenabled", string.Empty);
            var currentCompatLayer = config.ReadString(SettingSectionName, "compatlayer", string.Empty);
            if (string.IsNullOrWhiteSpace(currentCompatLayer) && !string.IsNullOrWhiteSpace(legacyCompatLayer))
            {
                config.WriteString(SettingSectionName, "compatlayer", legacyCompatLayer);
            }

            var legacyRenderMode = config.ReadString(SettingSectionName, "rendermode", string.Empty);
            var currentRenderMode = config.ReadString(SettingSectionName, "cl-rendermode", string.Empty);
            if (string.IsNullOrWhiteSpace(currentRenderMode) && !string.IsNullOrWhiteSpace(legacyRenderMode))
            {
                config.WriteString(SettingSectionName, "cl-rendermode", legacyRenderMode);
            }

            var legacyNoRestore = config.ReadString(DisplaySectionName, "norestorerotation", string.Empty);
            if (string.IsNullOrWhiteSpace(legacyNoRestore))
            {
                legacyNoRestore = config.ReadString(SettingSectionName, "norestorerotation", string.Empty);
            }

            var existingExitRestore = config.ReadString(DisplaySectionName, "exitrestore", string.Empty);
            if (string.IsNullOrWhiteSpace(existingExitRestore) && !string.IsNullOrWhiteSpace(legacyNoRestore))
            {
                bool noRestore = bool.TryParse(legacyNoRestore, out var parsedNoRestore) && parsedNoRestore;
                config.WriteString(DisplaySectionName, "exitrestore", (!noRestore).ToString().ToLowerInvariant());
            }

            config.DeleteKey(SettingSectionName, "portablemode");
            config.DeleteKey(SettingSectionName, "usepreconfig");
            config.DeleteKey(SettingSectionName, "contentsoverride");
            config.DeleteKey(SettingSectionName, "asphyxiaoverride");

            EnsureDefaults(config);
        }

        private static void EnsureDefaults(IConfigHandler config)
        {
            EnsureDefault(config, SettingSectionName, "noasphyxia", "false");
            EnsureDefault(config, SettingSectionName, "compatlayer", "false");
            EnsureDefault(config, SettingSectionName, "cl-rendermode", "dx9on12");
            EnsureDefault(config, SettingSectionName, "use-system-config", "false");

            EnsureDefault(config, DisplaySectionName, "displayconfigure", "false");
            EnsureDefault(config, DisplaySectionName, "exitrestore", "true");
            EnsureDefault(config, DisplaySectionName, "mode", "single");
            EnsureDefault(config, DisplaySectionName, "mainscreen", "0");
            EnsureDefault(config, DisplaySectionName, "subscreen", "0");
            EnsureDefault(config, DisplaySectionName, "subrotation", "0");
            EnsureDefault(config, DisplaySectionName, "mainrotation", "0");
            EnsureDefault(config, DisplaySectionName, "mainresolution", "640x480");
            EnsureDefault(config, DisplaySectionName, "subresolution", "640x480");
            EnsureDefault(config, DisplaySectionName, "mainrefresh", "59");
            EnsureDefault(config, DisplaySectionName, "subrefresh", "59");
        }

        private static void EnsureDefault(IConfigHandler config, string section, string key, string defaultValue)
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
                "noasphyxia = \"false\"",
                "compatlayer = \"false\"",
                "cl-rendermode = \"dx9on12\"",
                "use-system-config = \"false\"",
                string.Empty,
                "[Display]",
                "displayconfigure = \"false\"",
                "exitrestore = \"true\"",
                "mode = \"single\"",
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

            File.WriteAllText(configPath, string.Join(SystemEnvironment.NewLine, lines), TomlTextShared.Utf8NoBom);
        }
    }
}
