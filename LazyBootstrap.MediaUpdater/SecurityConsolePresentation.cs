using System;
using System.Runtime.InteropServices;

namespace LazyBootstrap.MediaUpdater
{
    internal static class SecurityConsolePresentation
    {
        private const int StdOutputHandle = -11;
        private const uint EnableVirtualTerminalProcessing = 4;

        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr handle, uint mode);

        internal static void ShowBlockedDriveWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                message = LazyBootstrap.MediaUpdate.MediaUpdateSecurity.BlockedNonGamePathMessage;
            }

            IntPtr stdout = GetStdHandle(StdOutputHandle);
            if (stdout != IntPtr.Zero && stdout != InvalidHandleValue)
            {
                TryEnableVirtualTerminal(stdout);
            }

            ConsoleColor prevFg = Console.ForegroundColor;
            try
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.Write("\x1b[1m\x1b[91m");
                Console.ForegroundColor = ConsoleColor.Red;

                Console.WriteLine(message);
                Console.WriteLine();
                Console.WriteLine("按任意键继续…");

                Console.Write("\x1b[0m");
                Console.ForegroundColor = prevFg;

                if (Environment.UserInteractive && !Console.IsInputRedirected)
                {
                    _ = Console.ReadKey(intercept: true);
                }
            }
            finally
            {
                Console.ForegroundColor = prevFg;
            }
        }

        private static void TryEnableVirtualTerminal(IntPtr stdout)
        {
            try
            {
                if (GetConsoleMode(stdout, out uint mode))
                {
                    SetConsoleMode(stdout, mode | EnableVirtualTerminalProcessing);
                }
            }
            catch
            {
            }
        }
    }
}
