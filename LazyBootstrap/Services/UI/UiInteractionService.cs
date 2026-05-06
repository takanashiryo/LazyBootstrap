using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace LazyBootstrap.Services.UI
{
    public interface IUiInteractionService
    {
        void AttachWindow(Window window);

        void DetachWindow(Window window);

        void ShowInfoToast(string title, string content);

        void ShowWarningToast(string title, string content);

        void ShowErrorToast(string title, string content);

        Task<bool> ShowDialogAsync(
            string title,
            object content,
            string confirmText,
            string cancelText,
            NotificationType type = NotificationType.Information,
            string confirmButtonClasses = "Flat",
            string cancelButtonClasses = null,
            string customIconAssetName = null);

        Task<string> PickFolderAsync(string title);

        Task<string> PickFileAsync(string title, IReadOnlyList<string> patterns);

        void MinimizeAttachedWindow();

        void RestoreAttachedWindow();
    }

    internal sealed class UiInteractionService : IUiInteractionService
    {
        private readonly ISukiDialogManager _dialogManager;
        private readonly ISukiToastManager _toastManager;
        private static Bitmap _informationDialogIconCache;
        private static Bitmap _warningDialogIconCache;
        private static Bitmap _errorDialogIconCache;
        private Window _window;

        public UiInteractionService(
            ISukiDialogManager dialogManager,
            ISukiToastManager toastManager)
        {
            _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
            _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
        }

        public void AttachWindow(Window window)
        {
            _window = window;
        }

        public void DetachWindow(Window window)
        {
            if (ReferenceEquals(_window, window))
            {
                _window = null;
            }
        }

        public void ShowInfoToast(string title, string content)
        {
            CreateToast(title, content, NotificationType.Information, 3);
        }

        public void ShowWarningToast(string title, string content)
        {
            CreateToast(title, content, NotificationType.Warning, 4);
        }

        public void ShowErrorToast(string title, string content)
        {
            CreateToast(title, content, NotificationType.Error, 4);
        }

        public Task<bool> ShowDialogAsync(
            string title,
            object content,
            string confirmText,
            string cancelText,
            NotificationType type = NotificationType.Information,
            string confirmButtonClasses = "Flat",
            string cancelButtonClasses = null,
            string customIconAssetName = null)
        {
            var builder = _dialogManager
                .CreateDialog()
                .OfType(type)
                .WithTitle(title ?? string.Empty)
                .WithContent(content);

            if (string.IsNullOrWhiteSpace(cancelButtonClasses))
            {
                builder.WithYesNoResult(confirmText ?? "确定", cancelText ?? "取消", confirmButtonClasses ?? "Flat");
            }
            else
            {
                builder.Completion = new TaskCompletionSource<bool>();
                builder.WithActionButton(confirmText ?? "确定", _ => builder.Completion.SetResult(true), true, SplitClasses(confirmButtonClasses ?? "Flat"));
                builder.WithActionButton(cancelText ?? "取消", _ => builder.Completion.SetResult(false), true, SplitClasses(cancelButtonClasses));
            }

            builder.Dismiss().ByClickingBackground();

            ApplyDialogNotificationIcon(builder, type, customIconAssetName);
            return builder.TryShowAsync();
        }

        public async Task<string> PickFolderAsync(string title)
        {
            if (_window?.StorageProvider == null)
            {
                return string.Empty;
            }

            var folders = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title ?? string.Empty,
                AllowMultiple = false
            });

            if (folders == null || folders.Count == 0)
            {
                return string.Empty;
            }

            return PathHelper.NormalizePath(folders[0].TryGetLocalPath());
        }

        public async Task<string> PickFileAsync(string title, IReadOnlyList<string> patterns)
        {
            if (_window?.StorageProvider == null)
            {
                return string.Empty;
            }

            var options = new FilePickerOpenOptions
            {
                Title = title ?? string.Empty,
                AllowMultiple = false
            };

            if (patterns != null && patterns.Count > 0)
            {
                options.FileTypeFilter =
                [
                    new FilePickerFileType("Supported Files")
                    {
                        Patterns = patterns
                    }
                ];
            }

            var files = await _window.StorageProvider.OpenFilePickerAsync(options);
            if (files == null || files.Count == 0)
            {
                return string.Empty;
            }

            return PathHelper.NormalizePath(files[0].TryGetLocalPath());
        }

        public void MinimizeAttachedWindow()
        {
            if (_window == null)
            {
                return;
            }

            Dispatcher.UIThread.Post(() => _window.WindowState = WindowState.Minimized);
        }

        public void RestoreAttachedWindow()
        {
            if (_window == null)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (_window.WindowState == WindowState.Minimized)
                {
                    _window.WindowState = WindowState.Normal;
                }

                _window.Show();
                _window.Activate();
            });
        }

        private void CreateToast(string title, string content, NotificationType type, int seconds)
        {
            _toastManager.CreateToast()
                .WithTitle(title ?? string.Empty)
                .WithContent(content ?? string.Empty)
                .OfType(type)
                .Dismiss().After(TimeSpan.FromSeconds(seconds))
                .Dismiss().ByClicking()
                .Queue();
        }

        private static void ApplyDialogNotificationIcon(SukiDialogBuilder builder, NotificationType type, string customIconAssetName = null)
        {
            if (builder?.Dialog == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(customIconAssetName))
            {
                var customBitmap = TryLoadDialogNotificationBitmap(customIconAssetName);
                if (customBitmap != null)
                {
                    builder.Dialog.Icon = customBitmap;
                    builder.Dialog.IconColor = null;
                    return;
                }
            }

            var iconBitmap = ResolveDefaultDialogIcon(type);

            if (iconBitmap == null)
            {
                return;
            }

            builder.Dialog.Icon = iconBitmap;
            builder.Dialog.IconColor = null;
        }

        private static Bitmap ResolveDefaultDialogIcon(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Information:
                    if (_informationDialogIconCache == null)
                    {
                        _informationDialogIconCache = TryLoadDialogNotificationBitmap("info.png")
                            ?? (_warningDialogIconCache ??= TryLoadDialogNotificationBitmap("warning.png"));
                    }

                    return _informationDialogIconCache;

                case NotificationType.Warning:
                    return _warningDialogIconCache ??= TryLoadDialogNotificationBitmap("warning.png");

                case NotificationType.Error:
                    return _errorDialogIconCache ??= TryLoadDialogNotificationBitmap("error.png");

                default:
                    return null;
            }
        }

        private static Bitmap TryLoadDialogNotificationBitmap(string assetFileName)
        {
            try
            {
                var assetUri = new Uri($"avares://LazyBootstrap/Assets/Images/{assetFileName}");
                using var stream = AssetLoader.Open(assetUri);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }


        private static string[] SplitClasses(string classes)
        {
            return string.IsNullOrWhiteSpace(classes)
                ? Array.Empty<string>()
                : classes.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
