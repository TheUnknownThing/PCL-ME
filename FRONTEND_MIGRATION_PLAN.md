# Frontend Migration Plan

## Summary

Wave 2 left the runtime boundary in a good state: shared runtime/platform behavior now lives behind explicit `PCL.Core` seams, while the remaining portability blockers are concentrated in the WPF/VB launcher.

The next phase should not replace the frontend yet. The next boundary should be launcher workflow extraction: keep the current WPF views, but move non-visual launcher workflows behind service interfaces so a future frontend can reuse them.

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

- startup orchestration and environment warnings in `Plain Craft Launcher 2/Application.xaml.vb`
- crash-report assembly in `Plain Craft Launcher 2/Modules/Minecraft/ModCrash.vb`
- launch pre/post workflow in `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb`
- form startup sequencing in `Plain Craft Launcher 2/FormMain.xaml.vb`

These flows still combine:

- launcher state gathering
- environment inspection
- formatting/report assembly
- user-facing prompts and shell actions

A future frontend should only own the prompts and view transitions, not the workflow logic itself.

## Recommended Next Boundary

The next implementation phase should be **launcher workflow extraction without replacing WPF**.

Create launcher-facing services in `PCL.Core` for:

1. startup environment evaluation
2. crash-report environment snapshot assembly
3. launch preparation / post-launch side effects that do not require direct control access

Keep the following in the launcher as adapters:

- message boxes and hint presentation
- WPF dispatcher marshaling
- window activation / shell interop
- Explorer / browser opening

This boundary keeps the current launcher behavior intact while making the eventual frontend swap mostly a matter of replacing views and shell adapters.

## Execution Order

1. Extract crash-report environment assembly first.
   This already depends on launcher-readable runtime/system data and now has a clean system-environment facade in `PCL.Core`.
2. Extract startup environment checks next.
   Move environment warning evaluation into a core service that returns warning items, then let WPF decide how to show them.
3. Extract launch workflow orchestration after that.
   Keep Java/game process control in the current launcher initially, but move decision-making and reportable state assembly into reusable services.

## Acceptance Criteria

- WPF pages and controls remain unchanged in behavior.
- New launcher workflow services do not introduce `System.Windows` dependencies into `PCL.Core.Foundation`.
- The launcher becomes a consumer of workflow results plus shell adapters, not the owner of business logic assembly.
- After the workflow extraction phase, frontend migration planning can choose a replacement UI stack without reopening runtime seams.
