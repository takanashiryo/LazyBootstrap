using System;
using System.IO;

namespace LazyBootstrap.MediaUpdate
{
    public static class MediaUpdateArguments
    {
        public const string GameSwitch = "--game";
        public const string StagingSwitch = "--staging";

        public static bool TryParse(string[] args, out string gamePath, out string stagingPath, out string error)
        {
            gamePath = null;
            stagingPath = null;
            error = null;

            string g = null;
            string s = null;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, GameSwitch, StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryTakeValue(args, ref i, out g))
                    {
                        error = "缺少 --game 路径。";
                        return false;
                    }
                    continue;
                }

                if (string.Equals(a, StagingSwitch, StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryTakeValue(args, ref i, out s))
                    {
                        error = "缺少 --staging 路径。";
                        return false;
                    }
                    continue;
                }

                if (a.StartsWith("--game=", StringComparison.OrdinalIgnoreCase))
                {
                    g = a.Substring("--game=".Length).Trim();
                    continue;
                }

                if (a.StartsWith("--staging=", StringComparison.OrdinalIgnoreCase))
                {
                    s = a.Substring("--staging=".Length).Trim();
                    continue;
                }
            }

            if (string.IsNullOrWhiteSpace(g) || string.IsNullOrWhiteSpace(s))
            {
                error = "用法: MediaUpdater.exe --game <游戏根目录> --staging <update_tmp 目录>";
                return false;
            }

            try
            {
                gamePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(g.Trim('"')));
                stagingPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(s.Trim('"')));
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            return true;
        }

        private static bool TryTakeValue(string[] args, ref int i, out string value)
        {
            value = null;
            if (i + 1 >= args.Length)
            {
                return false;
            }

            i++;
            value = args[i];
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
