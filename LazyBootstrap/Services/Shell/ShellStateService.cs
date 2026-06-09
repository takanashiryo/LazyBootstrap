using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LazyBootstrap.Services.Shell
{
    public sealed class ShellStateService : INotifyPropertyChanged
    {
        private string _statusText = "就绪";
        private double _statusProgressValue;
        private bool _isStatusProgressVisible;
        private bool _isInteractionEnabled = true;
        private ShellPage _selectedPage = ShellPage.Launch;

        public event PropertyChangedEventHandler PropertyChanged;

        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value ?? string.Empty);
        }

        public double StatusProgressValue
        {
            get => _statusProgressValue;
            set => SetField(ref _statusProgressValue, value);
        }

        public bool IsStatusProgressVisible
        {
            get => _isStatusProgressVisible;
            set => SetField(ref _isStatusProgressVisible, value);
        }

        public bool IsInteractionEnabled
        {
            get => _isInteractionEnabled;
            set => SetField(ref _isInteractionEnabled, value);
        }

        public ShellPage SelectedPage
        {
            get => _selectedPage;
            set => SetField(ref _selectedPage, value);
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
