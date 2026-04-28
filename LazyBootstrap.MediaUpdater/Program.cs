using System;
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

            if (!MediaUpdateArguments.TryParse(args, out string gamePath, out string stagingPath, out string parseError))
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
    }
}
