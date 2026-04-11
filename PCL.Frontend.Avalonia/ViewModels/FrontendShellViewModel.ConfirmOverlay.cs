using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Controls;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private readonly SemaphoreSlim _confirmOverlaySemaphore = new(1, 1);

    private async Task<bool> ShowInAppConfirmationAsync(
        string title,
        string message,
        string confirmText,
        bool isDanger)
    {
        await _confirmOverlaySemaphore.WaitAsync().ConfigureAwait(false);

        var resultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var promptId = $"inline-confirm-{Guid.NewGuid():N}";
        var resolvedConfirmText = string.IsNullOrWhiteSpace(confirmText) ? "确定" : confirmText;

        var wasPromptOverlayOpen = _isPromptOverlayOpen;
        var existingTopPrompt = ActivePrompts.Count > 0 ? ActivePrompts[0] : null;
        var completionInvoked = 0;

        void Complete(bool result)
        {
            if (Interlocked.Exchange(ref completionInvoked, 1) != 0)
            {
                return;
            }

            resultSource.TrySetResult(result);
        }

        var prompt = new PromptCardViewModel(
            AvaloniaPromptLaneKind.Startup,
            promptId,
            title,
            message,
            source: "Action",
            severity: isDanger ? "Warning" : "Info",
            titleBrush: isDanger ? Brush.Parse("#D33232") : Brush.Parse("#0B5BCB"),
            accentBrush: isDanger ? Brush.Parse("#D33232") : Brush.Parse("#256A61"),
            backgroundBrush: isDanger ? Brush.Parse("#FFF1EA") : Brush.Parse("#EAF7F5"),
            options:
            [
                new PromptOptionViewModel("取消", string.Empty, PclButtonColorState.Normal, new ActionCommand(() => Complete(false))),
                new PromptOptionViewModel(
                    resolvedConfirmText,
                    string.Empty,
                    isDanger ? PclButtonColorState.Red : PclButtonColorState.Highlight,
                    new ActionCommand(() => Complete(true)))
            ]);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _activeConfirmOverlayMessage = message;
            ActivePrompts.Insert(0, prompt);
            RaisePropertyChanged(nameof(HasActivePrompts));
            RaisePropertyChanged(nameof(HasNoActivePrompts));
            RaisePropertyChanged(nameof(CurrentPrompt));
            RaisePropertyChanged(nameof(HasCurrentPrompt));
            RaisePropertyChanged(nameof(PromptOverlayMessage));
            SetPromptOverlayOpen(true);
        });

        bool result;
        try
        {
            result = await resultSource.Task.ConfigureAwait(false);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _activeConfirmOverlayMessage = null;
                ActivePrompts.Remove(prompt);
                RaisePropertyChanged(nameof(HasActivePrompts));
                RaisePropertyChanged(nameof(HasNoActivePrompts));
                RaisePropertyChanged(nameof(CurrentPrompt));
                RaisePropertyChanged(nameof(HasCurrentPrompt));
                RaisePropertyChanged(nameof(PromptOverlayMessage));

                if (existingTopPrompt is not null)
                {
                    var currentTopPrompt = ActivePrompts.Count > 0 ? ActivePrompts[0] : null;
                    if (currentTopPrompt != existingTopPrompt)
                    {
                        SetPromptOverlayOpen(true);
                    }
                    else if (!wasPromptOverlayOpen)
                    {
                        SetPromptOverlayOpen(false);
                    }
                }
                else
                {
                    SetPromptOverlayOpen(false);
                }
            });

            _confirmOverlaySemaphore.Release();
        }

        return result;
    }
}
