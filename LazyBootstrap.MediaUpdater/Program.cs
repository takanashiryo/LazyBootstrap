using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LazyBootstrap.MediaUpdate;

namespace LazyBootstrap.MediaUpdater
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;
            }
            catch
            {
            }

            if (args == null || args.Length == 0)
            {
                Console.Error.WriteLine("请通过启动器启动。");
                return 1;
            }

            if (!TryParseArguments(args, out string gamePath, out string stagingPath, out string parseError))
            {
                Console.Error.WriteLine(string.IsNullOrEmpty(parseError) ? "参数无效。" : parseError);
                return 2;
            }

            try
            {
                int exit = await MediaUpdateRunner.RunAsync(
                    gamePath,
                    stagingPath,
                    static line => Console.WriteLine(line),
                    onUpdateComplete: WriteUpdateCompleteInGreen,
                    onSecurityBlockUi: SecurityConsolePresentation.ShowBlockedDriveWarning).ConfigureAwait(true);
                return exit;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 3;
            }
        }

        private static void WriteUpdateCompleteInGreen()
        {
            ConsoleColor previous = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine();
                Console.WriteLine("Update Complete！");
            }
            finally
            {
                Console.ForegroundColor = previous;
            }
        }

        private static bool TryParseArguments(
            string[] args,
            out string gamePath,
            out string stagingPath,
            out string error)
        {
            gamePath = null;
            stagingPath = null;
            error = null;
            string rawGamePath = null;
            string rawStagingPath = null;

            for (var index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                if (string.Equals(argument, "--game", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryTakeArgumentValue(args, ref index, out rawGamePath))
                    {
                        error = "缺少 --game 路径。";
                        return false;
                    }

                    continue;
                }

                if (string.Equals(argument, "--staging", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryTakeArgumentValue(args, ref index, out rawStagingPath))
                    {
                        error = "缺少 --staging 路径。";
                        return false;
                    }

                    continue;
                }

                if (argument.StartsWith("--game=", StringComparison.OrdinalIgnoreCase))
                {
                    rawGamePath = argument.Substring("--game=".Length).Trim();
                    continue;
                }

                if (argument.StartsWith("--staging=", StringComparison.OrdinalIgnoreCase))
                {
                    rawStagingPath = argument.Substring("--staging=".Length).Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(rawGamePath) || string.IsNullOrWhiteSpace(rawStagingPath))
            {
                error = "用法: MediaUpdater.exe --game <游戏根目录> --staging <update_tmp 目录>";
                return false;
            }

            try
            {
                gamePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rawGamePath.Trim('"')));
                stagingPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rawStagingPath.Trim('"')));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryTakeArgumentValue(string[] args, ref int index, out string value)
        {
            value = null;
            if (index + 1 >= args.Length)
            {
                return false;
            }

            value = args[++index];
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
