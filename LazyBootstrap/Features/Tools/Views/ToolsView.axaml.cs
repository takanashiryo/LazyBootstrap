using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LazyBootstrap.Features.Tools.Views
{
    public partial class ToolsView : UserControl
    {
        private readonly ToolsOrchestrator _toolsWorkflowService = null!;

        public ToolsView()
        {
            InitializeComponent();
        }

        public ToolsView(ToolsOrchestrator toolsOrchestrator)
        {
            InitializeComponent();
            _toolsWorkflowService = toolsOrchestrator ?? throw new ArgumentNullException(nameof(toolsOrchestrator));
        }

        private async void OnClearCacheClick(object sender, RoutedEventArgs e) =>
            await _toolsWorkflowService.ClearCacheAsync();
        private async void OnAddFirewallRuleClick(object sender, RoutedEventArgs e) =>
            await _toolsWorkflowService.AddFirewallRuleAsync();
        private async void OnOpenAudioPanelClick(object sender, RoutedEventArgs e) =>
            await _toolsWorkflowService.OpenAudioPanelAsync();
        private async void OnInstallRuntimeClick(object sender, RoutedEventArgs e)
        {
            await _toolsWorkflowService.InstallRuntimeAsync();
        }
        private async void OnBackupSavedataClick(object sender, RoutedEventArgs e) =>
            await _toolsWorkflowService.BackupSavedataAsync();
        private async void OnImportSavedataClick(object sender, RoutedEventArgs e) =>
            await _toolsWorkflowService.ImportSavedataAsync();
        private async void OnMigrateSavedataClick(object sender, RoutedEventArgs e) =>
            await _toolsWorkflowService.MigrateSavedataAsync();
    }
}
