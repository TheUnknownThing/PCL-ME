using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Tasks;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using System.Runtime.InteropServices;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Workflows;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private Dictionary<AvaloniaPromptLaneKind, List<PromptCardViewModel>> BuildPromptCatalog(string scenario)
    {
        return new Dictionary<AvaloniaPromptLaneKind, List<PromptCardViewModel>>
        {
            [AvaloniaPromptLaneKind.Startup] = BuildVisibleStartupPromptCards(),
            [AvaloniaPromptLaneKind.Launch] = [],
            [AvaloniaPromptLaneKind.Crash] = []
        };
    }

    private PromptCardViewModel CreatePromptCard(AvaloniaPromptLaneKind lane, LauncherFrontendPrompt prompt)
    {
        return new PromptCardViewModel(
            lane,
            prompt.Id,
            _i18n.T(prompt.Title),
            _i18n.T(prompt.Message),
            prompt.Source.ToString(),
            prompt.Severity.ToString(),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning
                ? FrontendThemeResourceResolver.GetBrush("ColorBrushRedLight", "#D33232")
                : FrontendThemeResourceResolver.GetBrush("ColorBrush2", "#0B5BCB"),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning
                ? FrontendThemeResourceResolver.GetBrush("ColorBrushRedLight", "#D33232")
                : FrontendThemeResourceResolver.GetBrush("ColorBrush2", "#0B5BCB"),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning
                ? FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticDangerBackground", "#FFF1EA")
                : FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticInfoBackground", "#EDF5FF"),
            prompt.Options.Select((option, index) => new PromptOptionViewModel(
                _i18n.T(option.Label),
                string.Empty,
                ResolvePromptOptionColorType(prompt.Severity, index, prompt.Options.Count),
                new ActionCommand(() => _ = ApplyPromptOptionAsync(lane, prompt.Id, option)))).ToList());
    }

    private static PclButtonColorState ResolvePromptOptionColorType(
        LauncherFrontendPromptSeverity severity,
        int index,
        int optionCount)
    {
        if (index != 0)
        {
            return PclButtonColorState.Normal;
        }

        if (severity == LauncherFrontendPromptSeverity.Warning)
        {
            return PclButtonColorState.Red;
        }

        return PclButtonColorState.Highlight;
    }

    private void EnsureStartupPromptLane()
    {
        _promptCatalog[AvaloniaPromptLaneKind.Startup] = BuildVisibleStartupPromptCards();
    }

    private List<PromptCardViewModel> BuildVisibleStartupPromptCards()
    {
        return LauncherFrontendPromptService.BuildStartupPromptQueue(_startupPlan.StartupPlan, _startupPlan.Consent)
            .Where(prompt => !_dismissedStartupPromptIds.Contains(prompt.Id))
            .Select(prompt => CreatePromptCard(AvaloniaPromptLaneKind.Startup, prompt))
            .ToList();
    }

    private void EnsureLaunchPromptLane()
    {
        var launchPrompts = LauncherFrontendPromptService.BuildLaunchPromptQueue(
            _launchComposition.PrecheckResult,
            _launchComposition.SupportPrompt,
            _launchComposition.JavaCompatibilityPrompt,
            GetPendingJavaPrompt());
        _promptCatalog[AvaloniaPromptLaneKind.Launch] = launchPrompts
            .Where(prompt => !_dismissedLaunchPromptIds.Contains(prompt.Id))
            .Select(prompt => CreatePromptCard(AvaloniaPromptLaneKind.Launch, prompt))
            .ToList();
    }

    private static string BuildLaunchPromptContextKey(
        FrontendLaunchComposition launchComposition,
        string? instanceDirectory)
    {
        return string.Join(
            "|",
            instanceDirectory ?? string.Empty,
            launchComposition.InstanceName,
            launchComposition.SelectedProfile.Kind,
            launchComposition.SelectedProfile.UserName);
    }

    private void EnsureCrashPromptLane()
    {
        var crashPrompts = LauncherFrontendPromptService.BuildCrashPromptQueue(_activeCrashPlan.OutputPrompt);
        _promptCatalog[AvaloniaPromptLaneKind.Crash] = crashPrompts
            .Select(prompt => CreatePromptCard(AvaloniaPromptLaneKind.Crash, prompt))
            .ToList();
    }
}
