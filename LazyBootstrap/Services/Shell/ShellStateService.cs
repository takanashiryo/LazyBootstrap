using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LazyBootstrap.Services.Shell
{
    public enum ShellBusyPresentation
    {
        GlobalOverlay,
        NavigationLock,
        RuntimeProgress
    }

    public sealed class ShellStateService : INotifyPropertyChanged
    {
        private readonly object _sync = new object();
        private readonly List<BusyEntry> _busyEntries = new List<BusyEntry>();
        private int _nextBusyId;

        private string _statusText = "就绪";
        private bool _isGlobalBusy;
        private string _globalBusyText = string.Empty;
        private bool _isNavigationLocked;
        private bool _isRuntimeProgressBusy;
        private string _runtimeProgressText = string.Empty;
        private double _runtimeProgressValue;
        private ShellPage _selectedPage = ShellPage.Launch;

        public event PropertyChangedEventHandler PropertyChanged;

        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value ?? string.Empty);
        }

        public bool IsGlobalBusy
        {
            get => _isGlobalBusy;
            private set => SetField(ref _isGlobalBusy, value);
        }

        public string GlobalBusyText
        {
            get => _globalBusyText;
            private set => SetField(ref _globalBusyText, value ?? string.Empty);
        }

        public bool IsNavigationLocked
        {
            get => _isNavigationLocked;
            private set => SetField(ref _isNavigationLocked, value);
        }

        public bool IsRuntimeProgressBusy
        {
            get => _isRuntimeProgressBusy;
            private set => SetField(ref _isRuntimeProgressBusy, value);
        }

        public string RuntimeProgressText
        {
            get => _runtimeProgressText;
            private set => SetField(ref _runtimeProgressText, value ?? string.Empty);
        }

        public double RuntimeProgressValue
        {
            get => _runtimeProgressValue;
            private set => SetField(ref _runtimeProgressValue, Math.Clamp(value, 0d, 100d));
        }

        public ShellPage SelectedPage
        {
            get => _selectedPage;
            set => SetField(ref _selectedPage, value);
        }

        public ShellBusyLease BeginBusy(
            ShellBusyPresentation presentation,
            string text = "",
            double progressValue = 0d)
        {
            BusyEntry entry;
            lock (_sync)
            {
                entry = new BusyEntry(
                    ++_nextBusyId,
                    presentation,
                    text ?? string.Empty,
                    Math.Clamp(progressValue, 0d, 100d));
                _busyEntries.Add(entry);
            }

            RefreshBusyState();
            return new ShellBusyLease(this, entry.Id);
        }

        private void UpdateBusy(int id, string text, double? progressValue)
        {
            lock (_sync)
            {
                var entry = _busyEntries.FirstOrDefault(e => e.Id == id);
                if (entry == null)
                {
                    return;
                }

                if (text != null)
                {
                    entry.Text = text;
                }

                if (progressValue.HasValue)
                {
                    entry.ProgressValue = Math.Clamp(progressValue.Value, 0d, 100d);
                }
            }

            RefreshBusyState();
        }

        private void EndBusy(int id)
        {
            bool changed;
            lock (_sync)
            {
                changed = _busyEntries.RemoveAll(e => e.Id == id) > 0;
            }

            if (changed)
            {
                RefreshBusyState();
            }
        }

        private void RefreshBusyState()
        {
            BusySnapshot snapshot;
            lock (_sync)
            {
                snapshot = BusySnapshot.FromEntries(_busyEntries);
            }

            IsGlobalBusy = snapshot.IsGlobalBusy;
            GlobalBusyText = snapshot.GlobalBusyText;
            IsNavigationLocked = snapshot.IsNavigationLocked;
            IsRuntimeProgressBusy = snapshot.IsRuntimeProgressBusy;
            RuntimeProgressText = snapshot.RuntimeProgressText;
            RuntimeProgressValue = snapshot.RuntimeProgressValue;
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

        private sealed class BusyEntry
        {
            public BusyEntry(int id, ShellBusyPresentation presentation, string text, double progressValue)
            {
                Id = id;
                Presentation = presentation;
                Text = text;
                ProgressValue = progressValue;
            }

            public int Id { get; }
            public ShellBusyPresentation Presentation { get; }
            public string Text { get; set; }
            public double ProgressValue { get; set; }
        }

        private sealed class BusySnapshot
        {
            public bool IsGlobalBusy { get; private init; }
            public string GlobalBusyText { get; private init; } = string.Empty;
            public bool IsNavigationLocked { get; private init; }
            public bool IsRuntimeProgressBusy { get; private init; }
            public string RuntimeProgressText { get; private init; } = string.Empty;
            public double RuntimeProgressValue { get; private init; }

            public static BusySnapshot FromEntries(IReadOnlyList<BusyEntry> entries)
            {
                var global = entries.LastOrDefault(e => e.Presentation == ShellBusyPresentation.GlobalOverlay);
                var runtime = entries.LastOrDefault(e => e.Presentation == ShellBusyPresentation.RuntimeProgress);

                return new BusySnapshot
                {
                    IsGlobalBusy = global != null,
                    GlobalBusyText = global?.Text ?? string.Empty,
                    IsNavigationLocked = entries.Any(e => e.Presentation == ShellBusyPresentation.NavigationLock),
                    IsRuntimeProgressBusy = runtime != null,
                    RuntimeProgressText = runtime?.Text ?? string.Empty,
                    RuntimeProgressValue = runtime?.ProgressValue ?? 0d
                };
            }
        }

        public sealed class ShellBusyLease : IDisposable
        {
            private readonly ShellStateService _owner;
            private readonly int _id;
            private bool _disposed;

            internal ShellBusyLease(ShellStateService owner, int id)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _id = id;
            }

            public void UpdateText(string text)
            {
                if (_disposed)
                {
                    return;
                }

                _owner.UpdateBusy(_id, text ?? string.Empty, null);
            }

            public void UpdateProgress(string text, double progressValue)
            {
                if (_disposed)
                {
                    return;
                }

                _owner.UpdateBusy(_id, text ?? string.Empty, progressValue);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.EndBusy(_id);
            }
        }
    }
}
