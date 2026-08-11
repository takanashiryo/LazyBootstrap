using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using LazyBootstrap.FileSystem;

namespace LazyBootstrap.UI
{
    public partial class MainWindow
    {
        private static Bitmap _informationDialogIconCache;
        private static Bitmap _warningDialogIconCache;
        private static Bitmap _errorDialogIconCache;

        private void ShowInfoToast(string title, string content)
        {
            CreateToast(title, content, NotificationType.Information, 3);
        }

        private void ShowWarningToast(string title, string content)
        {
            CreateToast(title, content, NotificationType.Warning, 4);
        }

        private void ShowErrorToast(string title, string content)
        {
            CreateToast(title, content, NotificationType.Error, 4);
        }

        private Task<bool> ShowDialogAsync(
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

        private Task ShowMessageDialogAsync(
            string title,
            object content,
            string buttonText,
            NotificationType type = NotificationType.Information,
            string buttonClasses = "Flat",
            string customIconAssetName = null)
        {
            var builder = _dialogManager
                .CreateDialog()
                .OfType(type)
                .WithTitle(title ?? string.Empty)
                .WithContent(content);

            builder.Completion = new TaskCompletionSource<bool>();
            builder.WithActionButton(
                buttonText ?? "确定",
                _ => builder.Completion.TrySetResult(true),
                true,
                SplitClasses(buttonClasses ?? "Flat"));

            ApplyDialogNotificationIcon(builder, type, customIconAssetName);
            return builder.TryShowAsync();
        }

        private async Task<string> PickFolderAsync(string title)
        {
            if (StorageProvider == null)
            {
                return string.Empty;
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title ?? string.Empty,
                AllowMultiple = false
            });

            return folders == null || folders.Count == 0
                ? string.Empty
                : PathHelper.NormalizePath(folders[0].TryGetLocalPath());
        }

        private async Task<string> PickFileAsync(string title, IReadOnlyList<string> patterns)
        {
            if (StorageProvider == null)
            {
                return string.Empty;
            }

            var options = new FilePickerOpenOptions
            {
                Title = title ?? string.Empty,
                AllowMultiple = false
            };

            if (patterns is { Count: > 0 })
            {
                options.FileTypeFilter =
                [
                    new FilePickerFileType("Supported Files")
                    {
                        Patterns = patterns
                    }
                ];
            }

            var files = await StorageProvider.OpenFilePickerAsync(options);
            return files == null || files.Count == 0
                ? string.Empty
                : PathHelper.NormalizePath(files[0].TryGetLocalPath());
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

        private static void ApplyDialogNotificationIcon(
            SukiDialogBuilder builder,
            NotificationType type,
            string customIconAssetName = null)
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
                    return _informationDialogIconCache ??= TryLoadDialogNotificationBitmap("info.png")
                        ?? (_warningDialogIconCache ??= TryLoadDialogNotificationBitmap("warning.png"));
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
