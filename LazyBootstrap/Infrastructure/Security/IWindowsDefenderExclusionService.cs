using System.Threading.Tasks;

namespace LazyBootstrap
{
    internal interface IWindowsDefenderExclusionService
    {
        Task<WindowsDefenderExclusionResult> EnsureDirectoryExcludedAsync(string directoryPath);
    }
}
