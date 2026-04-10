using System.Threading.Tasks;

namespace LazyBootstrap.Services.Security
{
    internal interface IWindowsDefenderExclusionService
    {
        Task<WindowsDefenderExclusionResult> EnsureDirectoryExcludedAsync(string directoryPath);
    }
}
