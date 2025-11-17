// written by Arkito aka Takanashi Ryo, only release in SDVX Lazy Pack.
using System;
using System.Windows.Forms;

namespace LazyBootstrap
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BootstrapForm());
        }
    }
}