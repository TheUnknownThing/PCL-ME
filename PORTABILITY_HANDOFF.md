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
  - now includes both CLI shell tooling and an Avalonia desktop shell spike
  - the desktop shell now has launcher-style top chrome, floating utility buttons, a copied launch surface, and grouped left navigation modeled after the existing WPF pages
  - copied non-launch right panes now also exist for `设置/关于`, `设置/反馈`, `设置/日志`, and `工具/帮助`
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
- frontend shell planning and navigation catalog generation
- frontend prompt queue and prompt-command mapping
- frontend shell desktop composition via the Avalonia spike
- launcher-style sidebar grouping, route-aware shell composition, and copied icon/control reuse inside the Avalonia spike
- first copied non-launch right-pane surfaces for about, feedback, log, and help pages
- page-specific frontend page-content seams for those copied settings/tools routes
- startup bootstrap and visual planning
- version transition and milestone policy
- launch prerun and launch session planning
- launch argument, classpath, native, and Java workflow planning
- Microsoft login request/step/failure workflow
- Authlib login request/step/failure workflow
- Java selection and transfer planning
- crash collection/export/prompt planning
- profile document parsing and serialization in `PCL.Core/Minecraft/Launch/MinecraftLaunchProfileStorageService.cs`
- launcher identity resolution in `PCL.Core/App/Essentials/LauncherIdentityResolutionService.cs`
- launcher secret-key resolution in `PCL.Core/App/Essentials/LauncherSecretKeyResolutionService.cs`
- launcher versioned secret-envelope parsing in `PCL.Core/App/Essentials/LauncherVersionedDataService.cs`
- launcher legacy identity derivation in `PCL.Core/App/Essentials/LauncherLegacyIdentityService.cs`
- launcher identity runtime resolution in `PCL.Core/App/Essentials/LauncherIdentityRuntimeService.cs`
- launcher legacy secret runtime resolution in `PCL.Core/App/Essentials/LauncherLegacySecretResolutionService.cs`
- launcher identity fallback in `PCL.Core/App/LauncherIdentity.cs`
- launcher data-protection runtime in `PCL.Core/App/LauncherDataProtectionRuntime.cs`
- encryption-key fallback in `PCL.Core/Utils/Secret/EncryptHelper.cs`
- Windows device identity extraction in `PCL.Core/Utils/Secret/WindowsDeviceIdentityProvider.cs`

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
- `671887f0` `refactor: extract profile storage service`
- `3cf04c43` `refactor: route profile persistence through backend storage`
- `dedeea7f` `refactor: make legacy secret migration explicit`
- `f14808ff` `feat: add portable frontend shell contracts`
- `491ee71d` `feat: add spike shell navigation prototype`
- `9125cb51` `refactor: extract launcher identity resolution`
- `b67430a9` `feat: add frontend subpage title lookup`
- `410563a7` `docs: polish shell spike contract output`
- `67f2665e` `refactor: isolate windows device identity adapter`
- `6e966b19` `refactor: extract legacy secret derivation service`
- `dfdd48d2` `refactor: extract secret key resolution services`
- `f3520e89` `feat: add frontend prompt and page surface contracts`
- `b9786c5c` `test: cover secret portability services`
- `6d4b7ff3` `feat: expose frontend shell artifacts in spike`
- `262bbb11` `refactor: extract launcher data protection service`
- `34746e39` `test: cover data protection storage seams`
- `4e01bcb7` `feat: add avalonia frontend shell spike`
- `ea9ab3d0` `docs: document desktop frontend shell spike`
- `408d8a4c` `refactor: route shell secret access through app runtime`
- `2674a878` `refactor: extract identity and legacy secret runtime services`
- `3ed64a54` `refactor: move encryption runtime into app layer`
- `8859224b` `feat: add route-aware frontend shell panels`
- `b2d7c574` `feat: align spike chrome with launcher routes`
- `cb7cdab0` `feat: copy launcher-style sidebar navigation`
- `dc20914d` `feat: simplify spike content cards`
- `9231a0be` `feat: copy setup and help shell surfaces`
- `7aea529a` `feat: add page-specific frontend content seams`

## Current Frontend Spike Status

The frontend migration is no longer just a shell frame.

Current Avalonia spike status:

- launch route now mirrors the current launcher's split left/right structure instead of a generic placeholder page
- non-launch routes now switch through a route-aware shell body rather than always rendering the launch page
- top chrome now switches between top-level tabs and inner back-title mode, matching the current launcher more closely
- non-launch left navigation now uses grouped launcher-style list items with copied labels, logos, and small right-side action buttons
- non-launch right content now uses a simpler `MyCard`-style stacked surface instead of spike-only dashboard chrome
- `设置/关于`, `设置/反馈`, `设置/日志`, and `工具/帮助` now render copied page-specific Avalonia layouts instead of the generic summary panel
- those copied routes now also have page-specific frontend page-content seams in `LauncherFrontendPageContentService`

What is still incomplete on the frontend side:

- many right-side route surfaces are still contract-driven summary cards, not copied page-specific WPF layouts yet
- setup still needs copied parity work for `启动`, `界面`, `游戏管理`, `联机`, `更新`, `Java`, and `启动器杂项`
- download, tools, and instance routes still need deeper page-by-page visual parity work beyond the newly copied help page
- some launcher controls are only approximated in Avalonia and should keep converging toward the WPF originals

## Frontend Migration Rule

This rule should be treated as explicit handoff guidance for the next frontend engineer:

- copy the existing launcher designs and controls whenever practical
- prefer reusing the current WPF page structure, spacing, grouping, iconography, and control hierarchy over inventing cleaner-looking replacements
- do not redesign a page because the backend contract is abstract; instead, render the contract inside a surface that still looks like the current launcher
- when a spike control is needed, model it after an existing control such as `MyCard`, `MyListItem`, `MyButton`, `MyIconButton`, or `MyExtraButton` before creating anything more generic
- when a page is still too generic, the next step is usually to copy more of the matching WPF layout, not to add new shell-specific chrome

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
- launcher identity resolution now lives in a dedicated service
- Windows device identity access is now isolated behind `WindowsDeviceIdentityProvider`
- profile document persistence is now routed through `MinecraftLaunchProfileStorageService`
- secret key resolution now lives in `LauncherSecretKeyResolutionService`
- versioned envelope parsing now lives in `LauncherVersionedDataService`
- legacy identity and legacy key derivation logic now live in `LauncherLegacyIdentityService`
- shell secret access now routes through app runtime services instead of reaching directly into the old helper path
- identity/runtime resolution now has dedicated runtime services instead of mixing persistence and derivation in one place
- encryption runtime ownership has moved into `PCL.Core/App/LauncherDataProtectionRuntime.cs`
- this area now has dedicated tests covering the new secret portability services
- data-protection storage seams now also have focused tests

What still remains:

- a final audit of remaining direct `Utils.Secret` assumptions outside the new services
- deciding whether any remaining secret/file-host operations should move behind a narrower adapter interface
- making profile/config secret access fully predictable for a future non-Windows frontend without depending on legacy helper knowledge

This is still one of the biggest backend-side architectural blockers, but it is now more isolated than before.

Current assessment:

- this backend track is close to done
- what remains is now more like final boundary hardening than large architectural extraction

### 3. Reduce leftover Windows helper leakage in `PCL.Core`

Current issue:

- some reusable code paths still rely on Windows-oriented helpers too directly

Needed outcome:

- backend-facing contracts are easier for a non-WPF frontend to consume
- reusable services stop depending on clipboard/dialog/process/UI assumptions by accident
- Windows-only behavior becomes clearly adapter-owned

### 4. Expand backend-consumable frontend seams beyond startup, prompt queues, and shell composition

`PCL.Frontend.Spike` now proves startup, navigation, prompt queues, shell artifact rendering, and a real Avalonia desktop shell composition, but the replacement frontend engineer will still benefit from additional stable seams for:

- richer page data contracts beyond the current catalog/view seam
- launch/profile/auth flow view models or request/response adapters
- crash presentation/export interactions
- page-specific right-pane content so copied WPF layouts do not have to fall back to generic summary text

This does not require re-centralizing WPF logic. It means making the backend and shell seams easier to consume from a new frontend.

## Two-Engineer Split

This is the recommended handoff structure.

### Engineer 1: frontend migration

Start from the current Avalonia spike, not from a blank window.

Priority order for the next frontend pass:

1. keep the copied launch route stable while expanding page-specific parity for the remaining setup right panes, starting with `更新` and the simpler settings cards
2. continue replacing generic non-launch content with copied WPF page structures, especially download auto-install and instance overview/setup pages
3. keep building Avalonia controls that explicitly mirror existing launcher controls such as `MySearchBox`, denser list rows, and richer card headers
4. ask the backend engineer for missing page data seams instead of compensating with frontend-only redesigns

Non-negotiable rule for this engineer:

- copy and reuse as much of the current launcher UI language as possible
- if forced to choose between "more generic but cleaner" and "closer to the current launcher", choose the latter for this migration phase

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

1. consume the existing `LauncherFrontendShellService` contract for startup plus navigation shell rendering
2. consume `LauncherFrontendPromptService` for startup, launch, and crash prompt rendering
3. build on the Avalonia shell spike instead of starting from zero
4. implement low-risk read-only or mostly-read-only pages
5. use the copied `设置/关于`, `设置/反馈`, `设置/日志`, and `工具/帮助` routes as the template for how future right panes should be migrated
6. request missing launch/profile/auth contracts instead of deriving them from WPF state

### Engineer 2: remaining backend and shell cleanup

This engineer should continue the portability cleanup.

Focus:

- tighten `PCL.Core` Windows-only boundaries
- finish `Utils.Secret` portability
- keep expanding frontend-consumable shell contracts where the new frontend hits gaps
- add any missing backend-facing contracts the frontend engineer uncovers
- keep `Plain Craft Launcher 2` moving toward a pure shell role instead of regressing into policy ownership

Secondary follow-up work:

- support the frontend engineer with page-specific surfaces
- only then return to any remaining WPF shell cleanup that is still worth separating

If you want this engineer to "finish backend next turn", define that as:

- finish the remaining backend portability cleanup
- close the remaining secret-boundary audit
- complete the next missing frontend-facing contracts discovered so far

Do not define it as:

- finishing the remaining WPF shell cleanup in `Application.xaml.vb` and `FormMain.xaml.vb`

Those files are now better treated as frontend-shell work, even if they still contain legacy coordination logic.

## Are We Ready To Start Frontend Migration?

Yes, with the right framing.

The honest assessment is:

- ready to start a frontend migration workstream: yes
- ready to cut over fully to a new frontend immediately: no

Why the answer is now yes:

- the old major blockers in `ModLaunch.vb`, `ModJava.vb`, and `ModCrash.vb` are no longer the dominant risk
- the frontend engineer now has an actual portable shell/navigation contract to build against
- the frontend engineer now also has portable prompt contracts to build against
- the spike now demonstrates shell-oriented startup and navigation output instead of only backend JSON
- the spike now emits frontend shell artifacts that are closer to a real replacement-shell integration target
- the frontend engineer now has an actual Avalonia desktop shell spike to iterate on
- `PCL.Core.Backend` is already the source of truth for most important portable workflows
- `PCL.Frontend.Spike` already proves that backend-driven non-WPF startup is viable
- the remaining work is now concentrated in startup/window shell cleanup, deeper page-level frontend contract coverage, and final secret portability cleanup, which can be handled in parallel with frontend development

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
