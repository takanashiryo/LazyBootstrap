using System;
using System.Collections.Generic;

namespace LazyBootstrap.Features.Launch.Services
{
    public static class SpiceCrashErrorCatalog
    {
        private static readonly IReadOnlyList<SpiceCrashErrorRule> Rules =
        [
            new SpiceCrashErrorRule(
                "AudioInitFailure",
                "音频初始化失败",
                "W:SuperstepSound: Audiodevice is not available!!!",
                "W:dll_entry_init: Failed to boot Audio."),

            new SpiceCrashErrorRule(
                "IncompleteGameData",
                "游戏数据不完整",
                "CreateLayer() 指定したレイヤーは存在しません"),

            new SpiceCrashErrorRule(
                "BadPcbid",
                "PCBID格式不正确",
                "F:ea3: boot: bad pcbid."),
            
            new SpiceCrashErrorRule(
                "MissingDependencies",
                "程序无法找到关键依赖文件",
                "Win32 error 126"),
            
            new SpiceCrashErrorRule(
                "DllLoadFailure",
                "游戏主程序加载失败，疑似损坏或系统架构不匹配",
                "Win32 error 193"),
            
            new SpiceCrashErrorRule(
                "MissingAvsConfig",
                "无法加载 prop/avs-config.xml",
                "F:avs-core: failed to open config file"),
            
            new SpiceCrashErrorRule(
                "MissingEa3Config",
                "无法加载 prop/ea3-config.xml",
                "F:avs-ea3: no ea3 config file found in prop directory"),
            
            new SpiceCrashErrorRule(
                "MissingSoftId",
                "无法检测到软件信息",
                "W:avs-ea3: soft id (datecode) not found in prop XML files")
        ];

        public static SpiceCrashErrorRule FindMatch(string logContent, out string matchedLine)
        {
            matchedLine = string.Empty;

            if (string.IsNullOrWhiteSpace(logContent))
            {
                return null;
            }

            string[] lines = logContent
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');

            foreach (var rule in Rules)
            {
                foreach (string marker in rule.Markers)
                {
                    if (string.IsNullOrWhiteSpace(marker))
                    {
                        continue;
                    }

                    foreach (string line in lines)
                    {
                        if (line.IndexOf(marker, StringComparison.Ordinal) >= 0)
                        {
                            matchedLine = line.Trim();
                            return rule;
                        }
                    }
                }
            }

            return null;
        }
    }
}
