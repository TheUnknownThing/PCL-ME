# PCL-CE Portability Handoff

## Executive Summary

This repo is ready to hand to two engineers working in parallel:

- one engineer can start the frontend migration now
- one engineer should continue the remaining backend and shell cleanup

The important nuance is:

- yes, the project is ready to start frontend migration work
- no, it is not ready for a full frontend cutover with zero backend follow-up

The repo is no longer in a "prove portability is possible" phase. The portable backend is real, the WPF shell has been heavily thinned, and the remaining work is now a finite set of shell and boundary cleanup tasks.

## Current Architecture

Use these boundaries as the current source of truth:

- `PCL.Core.Foundation`
  - portable primitives, regexes, helpers, utilities
  - no WPF ownership
- `PCL.Core.Backend`
  - portable workflow and policy backend
  - startup, launch, login, Java, crash, prompt-plan, and related orchestration logic belongs here by default
- `PCL.Core`
  - Windows compatibility and adapter layer
  - still contains some Windows-only helper leakage that should keep shrinking
- `Plain Craft Launcher 2`
  - current WPF frontend and Windows shell
  - should keep shrinking toward UI composition, prompts, windowing, routing, and OS actions
- `PCL.Frontend.Spike`
  - non-WPF proving ground
  - use it to validate backend contracts and bootstrap flows before or alongside a real replacement frontend

## What Has Been Finished

The major launcher-local extraction targets are no longer the blockers they were before.

### Minecraft launcher modules

Current thin-shell state:

- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb`
  - now down to roughly 457 lines
  - no longer the giant mixed policy hub it used to be
- `Plain Craft Launcher 2/Modules/Minecraft/ModJava.vb`
  - now down to roughly 72 lines
  - effectively a thin adapter for this migration phase
- `Plain Craft Launcher 2/Modules/Minecraft/ModCrash.vb`
  - now down to roughly 22 lines
  - crash analyzer has been split into partial shell files

Important extracted launch/crash shell modules now in place:

- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchExecutionShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchPrerunShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchArgumentShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchSessionArgumentShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchProfileShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchSessionPlanShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchPrecheckShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchJavaWorkflowShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchNativesShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchArgumentWorkflowShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchLoginModels.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchLoginWorkflowShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchArgumentModel.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModJavaPreferenceShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModCrashCollectionShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModCrashPrepareShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModCrashAnalysisShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModCrashResultShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModCrashExportShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModCrashPromptShell.vb`

### Startup / window / shell extraction

These shell modules already exist and should be treated as the continuation point:

- `Plain Craft Launcher 2/Modules/Base/ModApplicationRuntimeShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowLoadedShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowPresentationShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowShutdownShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowFocusShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowDragShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowWindowShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowChromeShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowPageAnimationShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowPageFrameShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowPageTitleShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowPageSelectionShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowSidebarShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowDragControlShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowNavigationShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowInputShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowPageNameShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowExtraButtonShell.vb`

### Backend and portability progress already established

These areas are already substantially portable or backend-owned:

- startup workflow planning
- startup bootstrap and visual planning
- version transition and milestone policy
- launch prerun and launch session planning
- launch argument, classpath, native, and Java workflow planning
- Microsoft login request/step/failure workflow
- Authlib login request/step/failure workflow
- Java selection and transfer planning
- crash collection/export/prompt planning
- launcher identity fallback in `PCL.Core/App/LauncherIdentity.cs`
- encryption-key fallback in `PCL.Core/Utils/Secret/EncryptHelper.cs`

## Recent Checkpoint Commits

These are the key migration checkpoints leading to the current state:

- `5b163bd1` `refactor: extract launch execution shell`
- `d6ff6d28` `refactor: extract launch prerun shell`
- `ff9f74ea` `refactor: extract launch argument shell helpers`
- `89d45e0d` `refactor: extract launch session argument shell`
- `e8e4eb0d` `refactor: extract launch profile and session planning shells`
- `df9f6883` `refactor: extract java preference selection shell`
- `542cf2a6` `refactor: extract crash collection and export shells`
- `092a5751` `refactor: finish launch workflow shell extraction`
- `2fe1c0a7` `refactor: finish launch login shell extraction`
- `9fe1317d` `refactor: split crash analyzer shells`

## What Still Remains Before A Full Frontend Cutover

These are the real remaining tracks. This list replaces the older stale blocker list.

### 1. Finish the startup and main-window shell boundary

Files:

- `Plain Craft Launcher 2/Application.xaml.vb`
- `Plain Craft Launcher 2/FormMain.xaml.vb`

Current state:

- `Application.xaml.vb` is already fairly small at roughly 122 lines
- `FormMain.xaml.vb` is much smaller in responsibility than before, but still around 897 lines

What still remains:

- remaining WPF event glue and lifecycle coordination
- page routing and page transition ownership
- prompt and consent thread wiring
- window composition, drag, DPI, and hook integration cleanup
- making the remaining frontend shell responsibilities explicit enough for a non-WPF frontend to mirror cleanly

This is now the biggest frontend-facing cleanup track.

### 2. Finish the secret / auth portability boundary

Area:

- `PCL.Core/Utils/Secret/*`
- related encrypted profile/config call sites

Key files:

- `PCL.Core/Utils/Secret/EncryptHelper.cs`
- `PCL.Core/Utils/Secret/Identify.cs`
- `PCL.Core/Utils/Secret/IdentifyOld.cs`

Progress already made:

- launcher identity has a portable fallback path
- encryption key storage has a portable fallback path

What still remains:

- a cleaner headless secret boundary
- reducing remaining assumptions around device identity and legacy secret handling
- making profile/config secret access predictable for a future non-Windows frontend

This is the biggest backend-side architectural blocker left.

### 3. Reduce leftover Windows helper leakage in `PCL.Core`

Current issue:

- some reusable code paths still rely on Windows-oriented helpers too directly

Needed outcome:

- backend-facing contracts are easier for a non-WPF frontend to consume
- reusable services stop depending on clipboard/dialog/process/UI assumptions by accident
- Windows-only behavior becomes clearly adapter-owned

### 4. Expand backend-consumable frontend seams beyond startup

`PCL.Frontend.Spike` currently proves startup consumption well enough to be useful, but the replacement frontend engineer will still benefit from additional stable seams for:

- prompt rendering models
- navigation/page data contracts
- launch/profile/auth flow view models or request/response adapters
- crash presentation/export interactions

This does not require re-centralizing WPF logic. It means making the backend and shell seams easier to consume from a new frontend.

## Two-Engineer Split

This is the recommended handoff structure.

### Engineer 1: frontend migration

This engineer can start now.

Focus:

- build the replacement frontend shell in parallel
- use `PCL.Frontend.Spike` and `PCL.Core.Backend` as the contract proving ground
- implement startup, consent, prompt, navigation, and page composition patterns without borrowing WPF-only behavior
- target low-risk flows first
- surface missing backend contracts clearly instead of re-implementing backend policy in the new frontend

Guardrails:

- do not port WPF code mechanically
- do not recreate launch/login/Java/crash policy in the frontend
- do not depend on `FormMain.xaml.vb` as a source of truth for domain logic

Recommended first frontend milestones:

1. startup bootstrap and consent flow using backend plans
2. window/app shell and navigation skeleton
3. prompt rendering abstractions
4. low-risk read-only or mostly-read-only pages
5. controlled integration of launch/profile/auth UI against backend contracts

### Engineer 2: remaining backend and shell cleanup

This engineer should continue the portability cleanup.

Focus:

- keep shrinking `Application.xaml.vb` and `FormMain.xaml.vb`
- tighten `PCL.Core` Windows-only boundaries
- finish `Utils.Secret` portability
- add any missing backend-facing contracts the frontend engineer uncovers
- keep `Plain Craft Launcher 2` moving toward a pure shell role instead of regressing into policy ownership

## Are We Ready To Start Frontend Migration?

Yes, with the right framing.

The honest assessment is:

- ready to start a frontend migration workstream: yes
- ready to cut over fully to a new frontend immediately: no

Why the answer is now yes:

- the old major blockers in `ModLaunch.vb`, `ModJava.vb`, and `ModCrash.vb` are no longer the dominant risk
- `PCL.Core.Backend` is already the source of truth for most important portable workflows
- `PCL.Frontend.Spike` already proves that backend-driven non-WPF startup is viable
- the remaining work is now concentrated in startup/window shell cleanup and secret portability, which can be handled in parallel with frontend development

What this means in practice:

- start the frontend now
- do not promise full frontend replacement without continued backend cleanup
- keep the frontend engineer building against explicit contracts, not WPF internals

## What Not To Do

Do not:

- move WPF controls/pages into the backend
- re-implement backend workflow logic in the new frontend
- reopen already-extracted backend workflow code unless fixing a bug or missing seam
- treat the old launcher modules as the long-term home for new portable policy
- wait for perfect backend cleanup before starting all frontend work

Also do not say:

- "nothing important is left before frontend migration"

Instead say:

- "the project is ready for parallel frontend migration and backend cleanup, but not for an immediate no-risk frontend cutover"

## Validation Commands

Run these before and after major changes:

```bash
dotnet build PCL.Core.Backend/PCL.Core.Backend.csproj -c Debug
dotnet test PCL.Core.Backend.Test/PCL.Core.Backend.Test.csproj -c Debug
dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- startup
dotnet build PCL.Core/PCL.Core.csproj -c Debug
dotnet build "Plain Craft Launcher 2/Plain Craft Launcher 2.vbproj" -c Debug
```

## Short Handoff Message

If you need a one-paragraph handoff:

`PCL.Core.Backend` is now the portable source of truth for most startup, launch, login, Java, and crash policy, and the old WPF launcher has been aggressively reduced into shell modules. `ModLaunch.vb`, `ModJava.vb`, and `ModCrash.vb` are no longer the primary blockers. The remaining work before a full frontend cutover is mainly the last startup/main-window shell cleanup in `Application.xaml.vb` and `FormMain.xaml.vb`, the unfinished `Utils.Secret` portability boundary, and reducing leftover Windows-only leakage in `PCL.Core`. You can now split the project between a frontend engineer building the replacement UI against backend contracts and a backend engineer finishing the remaining shell and portability cleanup in parallel.`
