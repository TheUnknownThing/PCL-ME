# Frontend Migration Plan

## Summary

Wave 2 left the runtime boundary in a good state, and the first launcher-workflow cleanup slices are now done: shared runtime/platform behavior lives behind explicit `PCL.Core` seams, startup environment warnings live in a core service, and crash-report environment assembly also lives in a core service.

That means the next engineer can move on to frontend migration work. The migration should still be compatibility-first: keep the current launcher behavior, reuse the existing core/runtime services, and replace launcher UI/shell layers incrementally instead of reopening runtime extraction.

## Completed Migration Prerequisites

These workflow extractions are already done and should be treated as available migration seams:

- GPU preference handling routes through `PCL.Core.Utils.OS.ProcessInterop`
- system environment summary is exposed through `PCL.Core.Utils.OS.SystemEnvironmentInfo`
- crash-report environment text is built by `PCL.Core.Minecraft.MinecraftCrashReportBuilder`
- startup environment warnings are evaluated by `PCL.Core.App.Essentials.LauncherStartupEnvironmentWarningService`

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

A future frontend should only own the prompts and view transitions, not the workflow logic itself.

## Recommended Next Boundary

The next implementation phase should be **frontend migration on top of the extracted workflow seams**.

Create launcher-facing services in `PCL.Core` for:

1. launch preparation / post-launch orchestration that does not require direct control access
2. startup shell flow models that return view-agnostic decisions/results instead of directly showing WPF UI
3. crash export workflow helpers that stop short of filesystem picker / shell opening concerns

Keep the following in the launcher as adapters:

- message boxes and hint presentation
- WPF dispatcher marshaling
- window activation / shell interop
- Explorer / browser opening

This boundary keeps the current launcher behavior intact while making the eventual frontend swap mostly a matter of replacing views and shell adapters.

## Execution Order

1. Extract launch workflow orchestration next.
   This is the highest-value remaining workflow seam and is still the biggest blocker to swapping frontend shells.
2. Introduce a frontend-agnostic presentation model for startup and crash-export flows.
   The evaluation/building logic is already in core; the next step is to stop binding shell flow directly to WPF windows and message boxes.
3. Start the actual frontend shell migration.
   Replace or parallel a small launcher surface first, while continuing to use existing `PCL.Core` services and Windows shell adapters as needed.

## Acceptance Criteria

- WPF pages and controls remain unchanged in behavior.
- New launcher workflow services do not introduce `System.Windows` dependencies into `PCL.Core.Foundation`.
- The launcher becomes a consumer of workflow results plus shell adapters, not the owner of business logic assembly.
- Frontend migration can proceed without reopening runtime seams or reintroducing launcher-local copies of runtime/system logic.
