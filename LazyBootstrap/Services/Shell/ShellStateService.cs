using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LazyBootstrap.Services.Shell
{
    public interface IShellStateService
    {
        string StatusText { get; set; }

        double StatusProgressValue { get; set; }

        bool IsStatusProgressVisible { get; set; }

        bool IsInteractionEnabled { get; set; }

        ShellPage SelectedPage { get; set; }

        event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    internal sealed partial class ShellStateService : ObservableObject, IShellStateService
    {
        [ObservableProperty]
        private string statusText = "就绪";

        [ObservableProperty]
        private double statusProgressValue;

        [ObservableProperty]
        private bool isStatusProgressVisible;

        [ObservableProperty]
        private bool isInteractionEnabled = true;

        [ObservableProperty]
        private ShellPage selectedPage = ShellPage.Launch;
    }
}
