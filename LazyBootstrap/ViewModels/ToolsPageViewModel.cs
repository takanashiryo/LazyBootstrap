using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LazyBootstrap.ViewModels
{
    public partial class ToolsPageViewModel : ObservableObject
    {
        private readonly IToolsWorkflowService _workflowService;

        public ToolsPageViewModel()
        {
        }

        public ToolsPageViewModel(IToolsWorkflowService workflowService)
        {
            _workflowService = workflowService;
        }

        [ObservableProperty]
        private bool isRuntimeInstallVisible;

        [ObservableProperty]
        private string runtimeStatusText = "正在准备安装运行库...";

        [ObservableProperty]
        private double runtimeProgressValue;

        public string RuntimeProgressDisplayText => $"{Math.Clamp((int)Math.Round(RuntimeProgressValue), 0, 100)}%";

        partial void OnRuntimeProgressValueChanged(double value)
        {
            OnPropertyChanged(nameof(RuntimeProgressDisplayText));
        }

        [RelayCommand]
        private Task ClearCacheAsync() => _workflowService?.ClearCacheAsync() ?? Task.CompletedTask;

        [RelayCommand]
        private Task AddFirewallRuleAsync() => _workflowService?.AddFirewallRuleAsync() ?? Task.CompletedTask;

        [RelayCommand]
        private Task OpenAudioPanelAsync() => _workflowService?.OpenAudioPanelAsync() ?? Task.CompletedTask;

        [RelayCommand]
        private Task InstallRuntimeAsync() => _workflowService?.InstallRuntimeAsync(this) ?? Task.CompletedTask;

        [RelayCommand]
        private Task BackupSavedataAsync() => _workflowService?.BackupSavedataAsync() ?? Task.CompletedTask;

        [RelayCommand]
        private Task ImportSavedataAsync() => _workflowService?.ImportSavedataAsync() ?? Task.CompletedTask;

        [RelayCommand]
        private Task MigrateSavedataAsync() => _workflowService?.MigrateSavedataAsync() ?? Task.CompletedTask;
    }
}
