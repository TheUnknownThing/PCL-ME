using System.Threading;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Dialogs;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private static readonly IReadOnlyList<PromptOptionViewModel> EmptyPromptOverlayOptions = [];
    private static readonly IReadOnlyList<PclChoiceDialogOption> EmptyPromptOverlayChoices = [];

    private readonly SemaphoreSlim _promptOverlayDialogSemaphore = new(1, 1);
    private PromptOverlayDialogState? _activePromptOverlayDialog;
    private PromptOverlayDialogState? _closingPromptOverlayDialog;
    private string _promptOverlayInputText = string.Empty;
    private string _closingPromptOverlayInputText = string.Empty;
    private string _promptOverlayInputPlaceholderText = string.Empty;
    private string _closingPromptOverlayInputPlaceholderText = string.Empty;
    private bool _promptOverlayInputIsPassword;
    private bool _closingPromptOverlayInputIsPassword;
    private PclChoiceDialogOption? _selectedPromptOverlayChoice;
    private PclChoiceDialogOption? _closingPromptOverlayChoice;

    public string PromptOverlayTitle => ResolvePromptOverlayPresentationDialog()?.Title ?? CurrentPrompt?.Title ?? string.Empty;

    public IBrush PromptOverlayTitleBrush => ResolvePromptOverlayPresentationDialog()?.TitleBrush
        ?? CurrentPrompt?.TitleBrush
        ?? FrontendThemeResourceResolver.GetBrush("ColorBrush2", "#0B5BCB");

    public IBrush PromptOverlayAccentBrush => ResolvePromptOverlayPresentationDialog()?.AccentBrush
        ?? CurrentPrompt?.AccentBrush
        ?? FrontendThemeResourceResolver.GetBrush("ColorBrush2", "#0B5BCB");

    public string PromptOverlayMessage => ResolvePromptOverlayPresentationDialog()?.Message ?? CurrentPrompt?.Message ?? string.Empty;

    public IReadOnlyList<PromptOptionViewModel> PromptOverlayOptions => ResolvePromptOverlayPresentationDialog()?.Options ?? CurrentPrompt?.Options ?? EmptyPromptOverlayOptions;

    public IReadOnlyList<PclChoiceDialogOption> PromptOverlayChoiceOptions => ResolvePromptOverlayPresentationDialog()?.ChoiceOptions ?? EmptyPromptOverlayChoices;

    public bool HasPromptOverlayInlineDialog => _activePromptOverlayDialog is not null;

    public bool ShowPromptOverlayTextInput => ResolvePromptOverlayPresentationDialog()?.Kind == PromptOverlayDialogKind.TextInput;

    public bool ShowPromptOverlayChoiceList => ResolvePromptOverlayPresentationDialog()?.Kind == PromptOverlayDialogKind.Choice;

    public bool ShowPromptOverlayMessage => !string.IsNullOrWhiteSpace(PromptOverlayMessage);

    public bool IsPromptOverlayVisible =>
        HasPromptOverlayInlineDialog ||
        !IsWelcomeOverlayVisible && HasActivePrompts && _isPromptOverlayOpen;

    public bool IsPromptOverlayWarning => ResolvePromptOverlayPresentationDialog()?.IsDanger
        ?? string.Equals(CurrentPrompt?.Severity, "Warning", StringComparison.OrdinalIgnoreCase);

    public string PromptOverlayInputText
    {
        get => _activePromptOverlayDialog is null && _closingPromptOverlayDialog is not null
            ? _closingPromptOverlayInputText
            : _promptOverlayInputText;
        set => SetProperty(ref _promptOverlayInputText, value);
    }

    public string PromptOverlayInputPlaceholderText => _activePromptOverlayDialog is null && _closingPromptOverlayDialog is not null
        ? _closingPromptOverlayInputPlaceholderText
        : _promptOverlayInputPlaceholderText;

    public char PromptOverlayInputPasswordChar => (_activePromptOverlayDialog is null && _closingPromptOverlayDialog is not null
        ? _closingPromptOverlayInputIsPassword
        : _promptOverlayInputIsPassword)
        ? '●'
        : '\0';

    public PclChoiceDialogOption? SelectedPromptOverlayChoice
    {
        get => _activePromptOverlayDialog is null && _closingPromptOverlayDialog is not null
            ? _closingPromptOverlayChoice
            : _selectedPromptOverlayChoice;
        set
        {
            if (!SetProperty(ref _selectedPromptOverlayChoice, value))
            {
                return;
            }

            _activePromptOverlayDialog?.ConfirmCommand.NotifyCanExecuteChanged();
        }
    }

    private PromptOverlayDialogState? ResolvePromptOverlayPresentationDialog()
    {
        return _activePromptOverlayDialog ?? _closingPromptOverlayDialog;
    }

    private bool ShouldFallbackToPromptOverlayContent()
    {
        return !IsWelcomeOverlayVisible && HasActivePrompts && _isPromptOverlayOpen;
    }

    private void CaptureClosingPromptOverlayPresentation(PromptOverlayDialogState dialog)
    {
        _closingPromptOverlayDialog = dialog;
        _closingPromptOverlayInputText = _promptOverlayInputText;
        _closingPromptOverlayInputPlaceholderText = _promptOverlayInputPlaceholderText;
        _closingPromptOverlayInputIsPassword = _promptOverlayInputIsPassword;
        _closingPromptOverlayChoice = _selectedPromptOverlayChoice;
    }

    private void ClearClosingPromptOverlayPresentation()
    {
        _closingPromptOverlayDialog = null;
        _closingPromptOverlayInputText = string.Empty;
        _closingPromptOverlayInputPlaceholderText = string.Empty;
        _closingPromptOverlayInputIsPassword = false;
        _closingPromptOverlayChoice = null;
    }

    internal bool TryHandlePromptOverlayKey(Key key)
    {
        switch (key)
        {
            case Key.Escape when _activePromptOverlayDialog is not null:
                _activePromptOverlayDialog.CancelCommand.Execute(null);
                return true;
            case Key.Enter when ResolvePromptOverlayPrimaryCommand() is { } command && command.CanExecute(null):
                command.Execute(null);
                return true;
            default:
                return false;
        }
    }

    private ActionCommand? ResolvePromptOverlayPrimaryCommand()
    {
        if (_activePromptOverlayDialog is not null)
        {
            return _activePromptOverlayDialog.ConfirmCommand;
        }

        var primaryOption = CurrentPrompt?.Options.FirstOrDefault(option => option.ColorType != PclButtonColorState.Normal)
            ?? CurrentPrompt?.Options.FirstOrDefault();
        return primaryOption?.Command;
    }

    internal void ConfirmPromptOverlayChoice()
    {
        if (_activePromptOverlayDialog?.Kind != PromptOverlayDialogKind.Choice)
        {
            return;
        }

        if (_activePromptOverlayDialog.ConfirmCommand.CanExecute(null))
        {
            _activePromptOverlayDialog.ConfirmCommand.Execute(null);
        }
    }

    private async Task<bool> ShowInAppConfirmationAsync(
        string title,
        string message,
        string confirmText,
        bool isDanger)
    {
        await _promptOverlayDialogSemaphore.WaitAsync().ConfigureAwait(false);

        var resultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolvedConfirmText = string.IsNullOrWhiteSpace(confirmText) ? T("common.actions.confirm") : confirmText;
        var completionInvoked = 0;

        void Complete(bool result)
        {
            if (Interlocked.Exchange(ref completionInvoked, 1) != 0)
            {
                return;
            }

            resultSource.TrySetResult(result);
        }

        var dialog = CreatePromptOverlayDialogState(
            PromptOverlayDialogKind.Confirm,
            title,
            message,
            isDanger,
            options:
            [
                new PromptOptionViewModel(T("common.actions.cancel"), string.Empty, PclButtonColorState.Normal, new ActionCommand(() => Complete(false))),
                new PromptOptionViewModel(
                    resolvedConfirmText,
                    string.Empty,
                    isDanger ? PclButtonColorState.Red : PclButtonColorState.Highlight,
                    new ActionCommand(() => Complete(true)))
            ]);

        return await ShowPromptOverlayDialogAsync(dialog, resultSource).ConfigureAwait(false);
    }

    private async Task<string?> ShowInAppTextInputAsync(
        string title,
        string message,
        string initialText,
        string confirmText,
        string? placeholderText,
        bool isPassword)
    {
        await _promptOverlayDialogSemaphore.WaitAsync().ConfigureAwait(false);

        var resultSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolvedConfirmText = string.IsNullOrWhiteSpace(confirmText) ? T("common.actions.confirm") : confirmText;
        var completionInvoked = 0;

        void Complete(string? result)
        {
            if (Interlocked.Exchange(ref completionInvoked, 1) != 0)
            {
                return;
            }

            resultSource.TrySetResult(result);
        }

        var dialog = CreatePromptOverlayDialogState(
            PromptOverlayDialogKind.TextInput,
            title,
            message,
            isDanger: false,
            options:
            [
                new PromptOptionViewModel(T("common.actions.cancel"), string.Empty, PclButtonColorState.Normal, new ActionCommand(() => Complete(null))),
                new PromptOptionViewModel(
                    resolvedConfirmText,
                    string.Empty,
                    PclButtonColorState.Highlight,
                    new ActionCommand(() => Complete(PromptOverlayInputText)))
            ]);

        return await ShowPromptOverlayDialogAsync(
            dialog,
            resultSource,
            initialInputText: initialText ?? string.Empty,
            inputPlaceholderText: placeholderText ?? string.Empty,
            isPassword: isPassword).ConfigureAwait(false);
    }

    private async Task<string?> ShowInAppChoiceAsync(
        string title,
        string message,
        IReadOnlyList<PclChoiceDialogOption> options,
        string? selectedId,
        string confirmText)
    {
        if (options.Count == 0)
        {
            return null;
        }

        await _promptOverlayDialogSemaphore.WaitAsync().ConfigureAwait(false);

        var resultSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolvedConfirmText = string.IsNullOrWhiteSpace(confirmText) ? T("common.actions.confirm") : confirmText;
        var completionInvoked = 0;
        PromptOverlayDialogState? dialog = null;

        void Complete(string? result)
        {
            if (Interlocked.Exchange(ref completionInvoked, 1) != 0)
            {
                return;
            }

            resultSource.TrySetResult(result);
        }

        var confirmCommand = new ActionCommand(
            () => Complete(SelectedPromptOverlayChoice?.Id),
            () => ReferenceEquals(_activePromptOverlayDialog, dialog) && SelectedPromptOverlayChoice is not null);
        dialog = CreatePromptOverlayDialogState(
            PromptOverlayDialogKind.Choice,
            title,
            message,
            isDanger: false,
            options:
            [
                new PromptOptionViewModel(T("common.actions.cancel"), string.Empty, PclButtonColorState.Normal, new ActionCommand(() => Complete(null))),
                new PromptOptionViewModel(
                    resolvedConfirmText,
                    string.Empty,
                    PclButtonColorState.Highlight,
                    confirmCommand)
            ],
            choiceOptions: options);

        var initialSelection = options.FirstOrDefault(option => string.Equals(option.Id, selectedId, StringComparison.Ordinal))
                               ?? options.FirstOrDefault();

        return await ShowPromptOverlayDialogAsync(
            dialog,
            resultSource,
            selectedChoice: initialSelection).ConfigureAwait(false);
    }

    private async Task<TResult> ShowPromptOverlayDialogAsync<TResult>(
        PromptOverlayDialogState dialog,
        TaskCompletionSource<TResult> resultSource,
        string? initialInputText = null,
        string? inputPlaceholderText = null,
        bool isPassword = false,
        PclChoiceDialogOption? selectedChoice = null)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _promptOverlayInputText = initialInputText ?? string.Empty;
            _promptOverlayInputPlaceholderText = inputPlaceholderText ?? string.Empty;
            _promptOverlayInputIsPassword = isPassword;
            _selectedPromptOverlayChoice = selectedChoice;
            SetActivePromptOverlayDialog(dialog);
            dialog.ConfirmCommand.NotifyCanExecuteChanged();
        });

        try
        {
            return await resultSource.Task.ConfigureAwait(false);
        }
        finally
        {
            var preserveClosingPresentation = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ReferenceEquals(_activePromptOverlayDialog, dialog))
                {
                    preserveClosingPresentation = !ShouldFallbackToPromptOverlayContent();
                    if (preserveClosingPresentation)
                    {
                        CaptureClosingPromptOverlayPresentation(dialog);
                    }

                    SetActivePromptOverlayDialog(null);
                }
                else if (_closingPromptOverlayDialog is not null && ReferenceEquals(_closingPromptOverlayDialog, dialog))
                {
                    preserveClosingPresentation = true;
                }

                _promptOverlayInputText = string.Empty;
                _promptOverlayInputPlaceholderText = string.Empty;
                _promptOverlayInputIsPassword = false;
                _selectedPromptOverlayChoice = null;
                RaisePromptOverlayPresentationProperties();
            });

            if (preserveClosingPresentation)
            {
                await Task.Delay(PclModalMotion.ExitDuration).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (ReferenceEquals(_closingPromptOverlayDialog, dialog))
                    {
                        ClearClosingPromptOverlayPresentation();
                        RaisePromptOverlayPresentationProperties();
                    }
                });
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_closingPromptOverlayDialog is not null && ReferenceEquals(_closingPromptOverlayDialog, dialog))
                    {
                        ClearClosingPromptOverlayPresentation();
                        RaisePromptOverlayPresentationProperties();
                    }
                });
            }

            _promptOverlayDialogSemaphore.Release();
        }
    }

    private PromptOverlayDialogState CreatePromptOverlayDialogState(
        PromptOverlayDialogKind kind,
        string title,
        string message,
        bool isDanger,
        IReadOnlyList<PromptOptionViewModel> options,
        IReadOnlyList<PclChoiceDialogOption>? choiceOptions = null)
    {
        var titleBrush = isDanger
            ? FrontendThemeResourceResolver.GetBrush("ColorBrushRedLight", "#D33232")
            : FrontendThemeResourceResolver.GetBrush("ColorBrush2", "#0B5BCB");
        return new PromptOverlayDialogState(
            kind,
            title,
            message,
            isDanger,
            titleBrush,
            titleBrush,
            options,
            choiceOptions ?? EmptyPromptOverlayChoices);
    }

    private void SetActivePromptOverlayDialog(PromptOverlayDialogState? dialog)
    {
        if (ReferenceEquals(_activePromptOverlayDialog, dialog))
        {
            RaisePromptOverlayPresentationProperties();
            return;
        }

        _activePromptOverlayDialog = dialog;
        RaisePromptOverlayPresentationProperties();
    }

    private void RaisePromptOverlayPresentationProperties()
    {
        RaisePropertyChanged(nameof(PromptOverlayTitle));
        RaisePropertyChanged(nameof(PromptOverlayTitleBrush));
        RaisePropertyChanged(nameof(PromptOverlayAccentBrush));
        RaisePropertyChanged(nameof(PromptOverlayMessage));
        RaisePropertyChanged(nameof(PromptOverlayOptions));
        RaisePropertyChanged(nameof(PromptOverlayChoiceOptions));
        RaisePropertyChanged(nameof(HasPromptOverlayInlineDialog));
        RaisePropertyChanged(nameof(ShowPromptOverlayTextInput));
        RaisePropertyChanged(nameof(ShowPromptOverlayChoiceList));
        RaisePropertyChanged(nameof(ShowPromptOverlayMessage));
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));
        NotifyTopLevelNavigationInteractionChanged();
        RaisePropertyChanged(nameof(IsPromptOverlayWarning));
        RaisePropertyChanged(nameof(PromptOverlayInputText));
        RaisePropertyChanged(nameof(PromptOverlayInputPlaceholderText));
        RaisePropertyChanged(nameof(PromptOverlayInputPasswordChar));
        RaisePropertyChanged(nameof(SelectedPromptOverlayChoice));
    }
}

internal enum PromptOverlayDialogKind
{
    Confirm = 0,
    TextInput = 1,
    Choice = 2
}

internal sealed class PromptOverlayDialogState(
    PromptOverlayDialogKind kind,
    string title,
    string message,
    bool isDanger,
    IBrush titleBrush,
    IBrush accentBrush,
    IReadOnlyList<PromptOptionViewModel> options,
    IReadOnlyList<PclChoiceDialogOption> choiceOptions)
{
    public PromptOverlayDialogKind Kind { get; } = kind;

    public string Title { get; } = title;

    public string Message { get; } = message;

    public bool IsDanger { get; } = isDanger;

    public IBrush TitleBrush { get; } = titleBrush;

    public IBrush AccentBrush { get; } = accentBrush;

    public IReadOnlyList<PromptOptionViewModel> Options { get; } = options;

    public IReadOnlyList<PclChoiceDialogOption> ChoiceOptions { get; } = choiceOptions;

    public ActionCommand ConfirmCommand => Options.Count > 0 ? Options[^1].Command : new ActionCommand(static () => { });

    public ActionCommand CancelCommand => Options.Count > 1 ? Options[0].Command : ConfirmCommand;
}
