using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.I18n;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.Processes;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed partial class FrontendShellActionService
{
    public async Task<string?> PickOpenFileAsync(string title, string typeName, params string[] patterns)
    {
        var storageProvider = TryGetStorageProvider(out var error)
            ?? throw new InvalidOperationException(error ?? "The current environment does not support file pickers.");
        var fileTypes = patterns.Length == 0
            ? null
            : new List<FilePickerFileType>
            {
                new(typeName)
                {
                    Patterns = patterns
                }
            };

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });
        return result.Count == 0 ? null : result[0].TryGetLocalPath();
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var storageProvider = TryGetStorageProvider(out var error)
            ?? throw new InvalidOperationException(error ?? "The current environment does not support folder pickers.");
        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return result.Count == 0 ? null : result[0].TryGetLocalPath();
    }

    public async Task<string?> PickSaveFileAsync(
        string title,
        string suggestedFileName,
        string typeName,
        string? suggestedStartFolder = null,
        params string[] patterns)
    {
        var storageProvider = TryGetStorageProvider(out var error)
            ?? throw new InvalidOperationException(error ?? "The current environment does not support file pickers.");
        var fileTypes = patterns.Length == 0
            ? null
            : new List<FilePickerFileType>
            {
                new(typeName)
                {
                    Patterns = patterns
                }
            };
        var startLocation = string.IsNullOrWhiteSpace(suggestedStartFolder)
            ? null
            : await storageProvider.TryGetFolderFromPathAsync(suggestedStartFolder);

        var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = Path.GetExtension(suggestedFileName),
            FileTypeChoices = fileTypes,
            SuggestedStartLocation = startLocation,
            ShowOverwritePrompt = true
        });
        return result?.TryGetLocalPath();
    }

    public async Task<string?> ReadClipboardTextAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.Clipboard is null)
        {
            throw new InvalidOperationException("The current environment does not support the clipboard.");
        }

        return await desktop.MainWindow.Clipboard.TryGetTextAsync();
    }

    public async Task SetClipboardTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.Clipboard is null)
        {
            throw new InvalidOperationException("The current environment does not support the clipboard.");
        }

        await desktop.MainWindow.Clipboard.SetTextAsync(text ?? string.Empty);
    }

    public async Task<string?> PromptForTextAsync(
        string title,
        string message,
        string initialText = "",
        string confirmText = "Confirm",
        string? placeholderText = null,
        bool isPassword = false)
    {
        if (TextInputPresenter is not null)
        {
            return await TextInputPresenter(title, message, initialText, confirmText, placeholderText, isPassword);
        }

        throw new InvalidOperationException("Text input dialogs require an in-app presenter.");
    }

    public async Task<string?> PromptForChoiceAsync(
        string title,
        string message,
        IReadOnlyList<PclChoiceDialogOption> options,
        string? selectedId = null,
        string confirmText = "Confirm")
    {
        if (options.Count == 0)
        {
            return null;
        }

        if (ChoicePresenter is not null)
        {
            return await ChoicePresenter(title, message, options, selectedId, confirmText);
        }

        throw new InvalidOperationException("Choice dialogs require an in-app presenter.");
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Confirm",
        bool isDanger = false)
    {
        if (ConfirmPresenter is not null)
        {
            return await ConfirmPresenter(title, message, confirmText, isDanger);
        }

        throw new InvalidOperationException("Confirm dialogs require an in-app presenter.");
    }

    private static IStorageProvider? TryGetStorageProvider(out string? error)
    {
        error = null;

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.StorageProvider is null)
        {
            error = "The current environment does not support file selection.";
            return null;
        }

        return desktop.MainWindow.StorageProvider;
    }

    private static global::Avalonia.Controls.Window GetDesktopMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
        {
            throw new InvalidOperationException("The current environment did not provide a main window.");
        }

        return desktop.MainWindow;
    }
}
