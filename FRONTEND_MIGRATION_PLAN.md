# Frontend Migration Plan

## Summary

Wave 2 left the runtime boundary in a good state, and a substantial set of launcher-workflow cleanup slices are now done: shared runtime/platform behavior lives behind explicit `PCL.Core` seams, startup environment warnings live in a core service, and crash-report environment assembly also lives in a core service.

That means the next engineer can keep pushing frontend-migration prep work without reopening runtime extraction. The migration should still be compatibility-first: keep the current launcher behavior, reuse the existing core/runtime services, finish the last high-value workflow extractions, and only then replace launcher UI/shell layers incrementally.

## Completed Migration Prerequisites

These workflow extractions are already done and should be treated as available migration seams:

- GPU preference handling routes through `PCL.Core.Utils.OS.ProcessInterop`
- system environment summary is exposed through `PCL.Core.Utils.OS.SystemEnvironmentInfo`
- crash-report environment text is built by `PCL.Core.Minecraft.MinecraftCrashReportBuilder`
- startup environment warnings are evaluated by `PCL.Core.App.Essentials.LauncherStartupEnvironmentWarningService`
- launch precheck prompt policy is owned by `PCL.Core.Minecraft.Launch.MinecraftLaunchPrecheckService`
- launch account prompt policy is owned by `PCL.Core.Minecraft.Launch.MinecraftLaunchAccountWorkflowService`
- launch Java requirement and missing-Java prompt policy are owned by `PCL.Core.Minecraft.Launch.MinecraftLaunchJavaRequirementService` and `PCL.Core.Minecraft.Launch.MinecraftLaunchJavaPromptService`
- third-party login failure policy is owned by `PCL.Core.Minecraft.Launch.MinecraftLaunchThirdPartyLoginWorkflowService`
- login profile mutation / cached-session reuse policy is owned by `PCL.Core.Minecraft.Launch.MinecraftLaunchLoginProfileWorkflowService`
- authlib login execution sequencing is owned by `PCL.Core.Minecraft.Launch.MinecraftLaunchThirdPartyLoginExecutionService`
- Microsoft login execution sequencing is owned by `PCL.Core.Minecraft.Launch.MinecraftLaunchMicrosoftLoginExecutionService`
- startup consent prompt policy is owned by `PCL.Core.App.Essentials.LauncherStartupConsentService`
- startup command parsing and startup preparation composition are owned by `PCL.Core.App.Essentials.LauncherStartupCommandService` and `LauncherStartupPreparationService`
- startup version-transition and version-isolation migration policy are owned by `PCL.Core.App.Essentials.LauncherVersionTransitionService` and `LauncherStartupVersionIsolationMigrationService`
- crash export packaging is owned by `PCL.Core.Minecraft.MinecraftCrashExportService`
- crash export request assembly is owned by `PCL.Core.Minecraft.MinecraftCrashExportWorkflowService`
- crash result prompt policy and export filename suggestion are owned by `PCL.Core.Minecraft.MinecraftCrashWorkflowService`
- post-launch launcher shell policy is owned by `PCL.Core.Minecraft.Launch.MinecraftLaunchShellService`
- launch-count support prompt policy is owned by `PCL.Core.Minecraft.Launch.MinecraftLaunchShellService`
- launch GPU-preference failure / admin-retry policy is owned by `PCL.Core.Minecraft.Launch.MinecraftLaunchGpuPreferenceWorkflowService`
- startup bootstrap policy is owned by `PCL.Core.App.Essentials.LauncherStartupBootstrapService`
- fatal log dialog presentation is routed through `PCL.Core.Logging.LogRuntimeHooks` instead of being hardcoded in `PCL.Core.Logging.LogService`

Do not redo these in the frontend migration branch; build on top of them.

## Remaining Launcher-Only Dependency Tracks

### 1. WPF-only UI shell and controls

These files are fundamentally presentation-layer code and should stay in the launcher until a replacement shell exists:

- `Plain Craft Launcher 2/Plain Craft Launcher 2.vbproj`
- `Plain Craft Launcher 2/FormMain.xaml.vb`
- `Plain Craft Launcher 2/Controls/*`
- `Plain Craft Launcher 2/Pages/*`

Scope in this track:

- XAML pages, custom controls, animations, markup extensions, effects, and dispatcher-driven UI flow
- window lifetime, splash screen, tooltip metadata, and visual theme application
- any code whose main job is manipulating WPF elements rather than computing launcher state

### 2. Windows shell / interop / registry glue

These files still bind the launcher directly to Win32 or Windows-only APIs:

- `Plain Craft Launcher 2/Application.xaml.vb`
- `Plain Craft Launcher 2/Modules/ModMain.vb`
- `Plain Craft Launcher 2/Modules/Base/ModBase.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModWatcher.vb`
- `Plain Craft Launcher 2/Controls/MyResizer.vb`
- `Plain Craft Launcher 2/Pages/PageTools/PageToolsTest.xaml.vb`

Main responsibilities still in this track:

- window activation and foreground control
- registry-backed launcher settings helpers
- memory optimization and privileged `NtInterop` tools
- window enumeration and title manipulation for launched Minecraft processes
- monitor / cursor / resize behavior implemented with raw Win32 calls

These should remain Windows adapters until a new frontend shell exists, but they should stop accumulating shared workflow logic.

### 3. Launcher workflows still mixed with presentation

This is the most important migration track because it blocks frontend replacement more than raw WPF controls do.

Current mixed areas include:

- launch pre/post workflow in `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb`
- form startup sequencing in `Plain Craft Launcher 2/FormMain.xaml.vb`
- remaining startup shell orchestration in `Plain Craft Launcher 2/Application.xaml.vb`
- crash-report export shell flow in `Plain Craft Launcher 2/Modules/Minecraft/ModCrash.vb`

These flows still combine:

- launcher state gathering
- user-facing prompts and shell actions
- WPF-specific navigation / dispatcher coordination
- some launch-specific decision logic

After the latest cleanup slices, the former biggest blocker has changed:

- login execution / orchestration is now mostly expressed through `PCL.Core` services, while `ModLaunch.vb` still owns request execution, prompt rendering, and shell/UI adapter work
- `ModCrash.vb` no longer decides crash-result dialog titles, button combinations, export archive naming, or export-request assembly; it still owns save-picker invocation, report zip creation, and Explorer opening
- `Application.xaml.vb` no longer assembles startup command parsing or warning/bootstrap composition; it still owns WPF startup shell work such as splash-screen display, tooltip metadata, memory optimization execution, and warning dialog rendering
- `FormMain.xaml.vb` no longer owns version-transition migration policy or version-isolation migration policy; it still owns WPF startup presentation and shell adapters
- `Program.vb` now reattaches the current fatal-dialog presentation behavior through a runtime hook instead of that behavior being hardcoded in `PCL.Core`

A future frontend should only own prompts, view transitions, and shell adapters, not the workflow logic itself.

## Recommended Next Boundary

The next implementation phase can move into a **small shell-replacement / frontend-contract spike on top of the extracted seams**.

Create launcher-facing services in `PCL.Core` for:

1. any leftover startup shell policy still assembled directly in launcher files
2. optional follow-up launch-step adapter cleanup only where `ModLaunch.vb` still mixes shell/UI work with reusable decision logic
3. a small shell-replacement spike that consumes the extracted services

Keep the following in the launcher as adapters:

- message boxes and hint presentation
- WPF dispatcher marshaling
- window activation / shell interop
- Explorer / browser opening

This boundary keeps the current launcher behavior intact while making the eventual frontend swap mostly a matter of replacing views and shell adapters.

## Execution Order

1. Start a small frontend-shell migration spike now that login execution / orchestration is no longer the main blocker.
   Prefer a narrow surface that can consume the extracted launch/startup/crash services without rewriting the whole launcher.
2. Trim startup shell policy in the launcher if it blocks that spike.
3. Keep refining `ModLaunch.vb` only where the new shell surface needs cleaner step-level adapters.
4. Trim any remaining `ModCrash.vb` picker / Explorer glue only if the shell spike needs it.

## Acceptance Criteria

- WPF pages and controls remain unchanged in behavior.
- New launcher workflow services do not introduce `System.Windows` dependencies into `PCL.Core.Foundation`.
- The launcher becomes a consumer of workflow results plus shell adapters, not the owner of business logic assembly.
- Frontend migration can proceed without reopening runtime seams or reintroducing launcher-local copies of runtime/system logic.
- The project can enter a small shell-migration phase now that `ModLaunch.vb` no longer owns the majority of login execution / orchestration policy.
