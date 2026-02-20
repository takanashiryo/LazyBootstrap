using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LazyBootstrap
{
    internal sealed class ServerPresetStore
    {
        private readonly string _configPath;

        public ServerPresetStore(string configPath)
        {
            _configPath = configPath;
        }

        public (List<ServerPresetItem> Presets, string ActivePreset, bool Mutated) Load(string nonePresetName, string asphyxiaPresetName, string asphyxiaDefaultUrl)
        {
            var presets = new List<ServerPresetItem>
            {
                new ServerPresetItem { Name = nonePresetName }
            };

            string activePreset = nonePresetName;
            bool hasPresetSection = false;
            bool mutated = false;

            if (File.Exists(_configPath))
            {
                var lines = File.ReadAllLines(_configPath, Encoding.UTF8);
                ServerPresetItem current = null;
                bool inServerSection = false;
                string fileActivePreset = string.Empty;

                void CommitCurrent()
                {
                    if (current == null || string.IsNullOrWhiteSpace(current.Name))
                    {
                        return;
                    }

                    if (presets.Any(p => string.Equals(p.Name, current.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }

                    presets.Add(current);
                }

                foreach (var raw in lines)
                {
                    var line = raw.Trim();

                    if (line.StartsWith("[Server]", StringComparison.OrdinalIgnoreCase))
                    {
                        inServerSection = true;
                        continue;
                    }

                    if (line.StartsWith("[[Server.Presets]]", StringComparison.OrdinalIgnoreCase)
                        || line.StartsWith("[[ServerPresets]]", StringComparison.OrdinalIgnoreCase))
                    {
                        hasPresetSection = true;
                        inServerSection = false;
                        CommitCurrent();
                        current = new ServerPresetItem();
                        continue;
                    }

                    if (inServerSection)
                    {
                        if (line.StartsWith("[", StringComparison.Ordinal) && !line.StartsWith("[[", StringComparison.Ordinal))
                        {
                            inServerSection = false;
                        }
                        else
                        {
                            if (TryParseTomlKeyValue(line, out var serverKey, out var serverValue)
                                && string.Equals(serverKey, "activepreset", StringComparison.OrdinalIgnoreCase))
                            {
                                fileActivePreset = serverValue;
                            }

                            continue;
                        }
                    }

                    if (current == null)
                    {
                        continue;
                    }

                    if (line.StartsWith("[", StringComparison.Ordinal) && !line.StartsWith("[[", StringComparison.Ordinal))
                    {
                        CommitCurrent();
                        current = null;
                        continue;
                    }

                    if (!TryParseTomlKeyValue(line, out var key, out var value))
                    {
                        continue;
                    }

                    if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        current.Name = value;
                    }
                    else if (string.Equals(key, "serverurl", StringComparison.OrdinalIgnoreCase))
                    {
                        current.ServerUrl = value;
                    }
                    else if (string.Equals(key, "pcbid", StringComparison.OrdinalIgnoreCase))
                    {
                        current.PcbId = value;
                    }
                }

                CommitCurrent();

                if (!string.IsNullOrWhiteSpace(fileActivePreset))
                {
                    var matched = presets.FirstOrDefault(p => string.Equals(p.Name, fileActivePreset, StringComparison.OrdinalIgnoreCase));
                    if (matched != null)
                    {
                        activePreset = matched.Name;
                    }
                }
            }

            var existingAsphyxia = presets.FirstOrDefault(p => string.Equals(p.Name, asphyxiaPresetName, StringComparison.OrdinalIgnoreCase));
            if (existingAsphyxia == null)
            {
                presets.Add(new ServerPresetItem
                {
                    Name = asphyxiaPresetName,
                    ServerUrl = asphyxiaDefaultUrl,
                    PcbId = string.Empty
                });
                mutated = true;
            }
            else if (string.IsNullOrWhiteSpace(existingAsphyxia.ServerUrl))
            {
                existingAsphyxia.ServerUrl = asphyxiaDefaultUrl;
                mutated = true;
            }

            if (!hasPresetSection)
            {
                mutated = true;
            }

            return (presets, activePreset, mutated);
        }

        public void Save(IEnumerable<ServerPresetItem> presets, string activePreset, string nonePresetName)
        {
            var lines = File.Exists(_configPath)
                ? File.ReadAllLines(_configPath, Encoding.UTF8).ToList()
                : new List<string>();

            var kept = new List<string>();
            bool skippingOldPresets = false;
            bool skippingServerSection = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("[Server]", StringComparison.OrdinalIgnoreCase))
                {
                    skippingServerSection = true;
                    continue;
                }

                if (trimmed.StartsWith("[[Server.Presets]]", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("[[ServerPresets]]", StringComparison.OrdinalIgnoreCase))
                {
                    skippingOldPresets = true;
                    continue;
                }

                if (skippingServerSection || skippingOldPresets)
                {
                    if (trimmed.StartsWith("[", StringComparison.Ordinal) && !trimmed.StartsWith("[[", StringComparison.Ordinal))
                    {
                        skippingServerSection = false;
                        skippingOldPresets = false;
                        kept.Add(line);
                    }
                    continue;
                }

                kept.Add(line);
            }

            while (kept.Count > 0 && string.IsNullOrWhiteSpace(kept[kept.Count - 1]))
            {
                kept.RemoveAt(kept.Count - 1);
            }

            if (kept.Count > 0)
            {
                kept.Add(string.Empty);
            }

            kept.Add("[Server]");
            kept.Add($"activepreset = \"{TomlTextShared.EscapeTomlString(activePreset ?? nonePresetName)}\"");

            foreach (var preset in presets.Where(p => !string.Equals(p.Name, nonePresetName, StringComparison.OrdinalIgnoreCase)))
            {
                kept.Add(string.Empty);
                kept.Add("[[Server.Presets]]");
                kept.Add($"name = \"{TomlTextShared.EscapeTomlString(preset.Name)}\"");
                kept.Add($"serverurl = \"{TomlTextShared.EscapeTomlString(preset.ServerUrl)}\"");
                kept.Add($"pcbid = \"{TomlTextShared.EscapeTomlString(preset.PcbId)}\"");
            }

            NormalizeBlankLines(kept);
            File.WriteAllText(_configPath, string.Join(Environment.NewLine, kept), TomlTextShared.Utf8NoBom);
        }

        private static void NormalizeBlankLines(List<string> lines)
        {
            TomlTextShared.NormalizeBlankLines(lines, preserveSectionSeparator: false);
        }

        private static bool TryParseTomlKeyValue(string line, out string key, out string value)
        {
            return TomlTextShared.TryParseTomlKeyValue(line, UnquoteTomlString, out key, out value);
        }

        private static string UnquoteTomlString(string rawValue)
        {
            var value = rawValue?.Trim() ?? string.Empty;
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                var inner = value.Substring(1, value.Length - 2);
                return inner
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\")
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\t", "\t");
            }

            return value;
        }

    }
}
