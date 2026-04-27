using System.Threading.Tasks;
using LazyBootstrap.ViewModels;

namespace LazyBootstrap.Services.Update
{
    public interface IUpdateWorkflowService
    {
        Task ApplyUpdateFromUserSelectedArchiveAsync(UpdatePageViewModel viewModel);
    }
}
