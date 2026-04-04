using PCL.Frontend.Spike.Desktop.Controls;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void ApplyToolsComposition(FrontendToolsComposition composition)
    {
        _toolsComposition = composition;
        _suppressToolsPersistence = true;
        try
        {
            InitializeToolsTestSurface();
        }
        finally
        {
            _suppressToolsPersistence = false;
        }
    }

    private void ReloadToolsComposition()
    {
        ApplyToolsComposition(FrontendToolsCompositionService.Compose(_shellActionService.RuntimePaths));
    }

    private void PersistToolsSetting(string? propertyName)
    {
        if (_suppressToolsPersistence || string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        switch (propertyName)
        {
            case nameof(ToolDownloadFolder):
                _shellActionService.PersistSharedValue("CacheDownloadFolder", ToolDownloadFolder);
                break;
            case nameof(ToolDownloadUserAgent):
                _shellActionService.PersistSharedValue("ToolDownloadCustomUserAgent", ToolDownloadUserAgent);
                break;
        }
    }

    private ActionCommand ResolveToolboxActionCommand(string actionKey, string title)
    {
        return actionKey switch
        {
            "crash-test" => new ActionCommand(TriggerCrashPromptTest),
            "memory-optimize" => CreateIntentCommand(title, "Would run the launcher memory optimization workflow."),
            "clear-rubbish" => CreateIntentCommand(title, "Would clear cache, logs, and crash reports."),
            "daily-luck" => CreateIntentCommand(title, "Would calculate the daily luck value."),
            "create-shortcut" => CreateIntentCommand(title, "Would create a shortcut to the launcher executable."),
            "launch-count" => CreateIntentCommand(title, "Would show the launcher start-count dialog."),
            _ => CreateIntentCommand(title, $"Would run the {title} toolbox action.")
        };
    }

    private ToolboxActionViewModel CreateToolboxAction(FrontendToolboxActionDefinition action)
    {
        return new ToolboxActionViewModel(
            action.Title,
            action.ToolTip,
            action.MinWidth,
            action.IsDanger ? PclButtonColorState.Red : PclButtonColorState.Normal,
            ResolveToolboxActionCommand(action.ActionKey, action.Title));
    }
}
