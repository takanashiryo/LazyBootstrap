using System;
using System.Windows.Forms;

namespace LazyBootstrap
{
    public static class LogSystem
    {
        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        private static RichTextBox _output;

        public static void Initialize(RichTextBox richTextBox)
        {
            _output = richTextBox;
        }

        public static void Log(string message)
        {
            Log(message, LogLevel.Info);
        }

        public static void Log(string message, LogLevel level)
        {
            if (_output == null || string.IsNullOrEmpty(message)) return;

            Action append = () =>
            {
                if (level == LogLevel.Warning || level == LogLevel.Error)
                {
                    _output.SelectionStart = _output.TextLength;
                    _output.SelectionLength = 0;
                    _output.SelectionColor = level == LogLevel.Error
                        ? System.Drawing.Color.Red
                        : System.Drawing.Color.Orange;
                    _output.AppendText(message + Environment.NewLine);
                    _output.SelectionColor = _output.ForeColor; // reset color
                }
                else
                {
                    _output.AppendText(message + Environment.NewLine);
                }
            };

            if (_output.InvokeRequired)
            {
                _output.Invoke((MethodInvoker)(() => append()));
            }
            else
            {
                append();
            }
        }
    }
}
