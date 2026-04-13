# Frontend Migration Plan

## Mission

The frontend migration is no longer about proving that a non-WPF shell is possible.

The new goal is:

- keep the copied launcher UI work already done in `PCL.Frontend.Spike`
- stop feeding that UI mostly from spike fixtures
- start wiring real backend-owned startup, navigation, prompt, launch, and page data into the frontend
- keep policy in backend services and keep the frontend responsible for composition, interaction, routing, and OS-facing actions only

The practical target is a frontend that looks like the current launcher, but gets its state from portable services instead of WPF page logic or spike-only sample factories.

## Status Update

Status as of 2026-04-04:

- Phase 1 is complete
- Phase 2 is complete
- Phase 3 is complete
- Phase 4A is complete
- Phase 4B is complete
- Phase 5 is complete as of 2026-04-04

Phase 1 completion means:

- shell bootstrap inputs are now composed from real runtime/config state instead of default spike shell fixtures
- the frontend has a dedicated composition layer in `PCL.Frontend.Spike/Workflows/FrontendShellCompositionService.cs`
- `FrontendShellViewModel` now consumes real startup, consent, navigation, startup-count, and task/runtime shell inputs
- `PCL.Core.Backend` now exposes the portable runtime pieces needed for encrypted startup count and task visibility

Phase 1 did not attempt to:

- redesign any copied Avalonia surface
- replace launch-page fixture plans
- replace crash-page fixture plans
- implement prompt action execution

## Current Repo Map

- `PCL.Core.Foundation`
  - portable low-level helpers and primitives
  - no WPF ownership
- `PCL.Core`
  - the main source tree
  - contains portable services, Windows adapters, configuration, UI infrastructure, and legacy compatibility code
- `PCL.Core.Backend`
  - a `net8.0` linked projection of portable files from `PCL.Core`
  - this is not a separate implementation tree; it is the portable backend slice of `PCL.Core`
- `PCL.Frontend.Spike`
  - the current replacement-frontend prototype
  - includes CLI inspection flows plus an Avalonia desktop shell
- `Plain Craft Launcher 2`
  - the legacy WPF shell
  - still the source of truth for visuals, interaction patterns, and route structure

## What Is Already Real

The project already has real portable backend services for the logic that matters.

### Shell and startup seams

- `PCL.Core/App/Essentials/LauncherFrontendNavigationService.cs`
- `LauncherFrontendShellService` inside `PCL.Core/App/Essentials/LauncherFrontendNavigationService.cs`
- `PCL.Core/App/Essentials/LauncherFrontendPromptService.cs`
- `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs`
- `PCL.Core/App/Essentials/LauncherStartupWorkflowService.cs`
- `PCL.Core/App/Essentials/LauncherStartupBootstrapService.cs`
- `PCL.Core/App/Essentials/LauncherMainWindowStartupWorkflowService.cs`
- `PCL.Core/App/Essentials/LauncherStartupShellService.cs`

These already define portable startup planning, shell navigation, utility surface visibility, prompt queue shape, and summary-level page content.

### Launch, login, Java, and crash workflow

The backend already contains portable services for:

- launch precheck, prerun, classpath, natives, arguments, resolution, process startup, and session logging
- Microsoft login request planning and execution
- Authlib and third-party login request planning and execution
- Java runtime requirement detection, selection, download planning, transfer planning, and session transitions
- crash prompt, export, archive, and response workflows

The relevant code is concentrated under:

- `PCL.Core/Minecraft/Launch/*.cs`
- `PCL.Core/Minecraft/MinecraftCrash*.cs`

### Portable tests

Regression coverage already exists for both shell seams and workflow logic:

- `PCL.Core.Test/App/*`
- `PCL.Core.Test/Minecraft/*`
- `PCL.Core.Backend.Test/*`

That means the next frontend step should be integration, not more speculative frontend-only modeling.

## What Is Still Prototype-Only

The frontend shell is visually far ahead of its data wiring.

### Bootstrap is no longer the main prototype-only gap

The Avalonia app currently starts here:

- `PCL.Frontend.Spike/Desktop/App.axaml.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Construction.cs`

Today shell bootstrap is composed through:

- `PCL.Frontend.Spike/Workflows/FrontendShellCompositionService.cs`
- `PCL.Frontend.Spike/Models/FrontendShellComposition.cs`
- `PCL.Core/App/Essentials/LauncherFrontendRuntimeStateService.cs`

That means the shell contract services are real and the shell bootstrap requests are now built from real runtime/config state.

### Many right panes are still copied visually but not backed by real page data

The Avalonia shell now has copied surfaces for:

- `设置/关于`, `设置/反馈`, `设置/日志`, `设置/更新`, `设置/游戏联机`, `设置/游戏管理`, `设置/启动器杂项`, `设置/Java`, `设置/界面`
- `下载/自动安装`
- download resource list routes
- `工具/联机大厅`, `工具/帮助`, `工具/测试`
- `实例/概览`, `实例/设置`, `实例/导出`, `实例/安装`, `实例/世界`, `实例/截图`, `实例/服务器`
- `实例/Mod`, `实例/已禁用 Mod`, `实例/资源包`, `实例/光影包`, `实例/投影原理图`

But most of those surfaces are still fed by:

- hard-coded view-model values in `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.*.cs`
- summary contracts in `LauncherFrontendPageContentService`
- spike-only commands that only write to the activity feed

### Prompt actions are now real shell actions, and the launch route is runtime-backed on the normal app path

Prompt rendering and prompt-command execution now both exist:

- prompt options map to `LauncherFrontendPromptCommandKind`
- prompt commands now drive config persistence, route changes, launch abort/continue state, crash export/log actions, Java materialization, and shell exit behavior
- prompt lanes now follow runtime lifecycle instead of appearing as a static always-on inbox

What is still missing is the state source behind many prompts and surfaces:

- crash and Java actions still materialize frontend-side artifacts rather than fully live launcher integrations
- many non-launch routes still rely on hard-coded page-local view-model state

Important caveat:

- `app --input-root ...` still intentionally uses replay-backed launch inputs for inspection scenarios

### The next missing middle is route-local page-state composition

The shell-level composition layer now exists.

The missing frontend layers are now:

- route-local page models derived from backend/state sources instead of fixtures
- continued separation of reusable frontend adapters from spike-only inspection helpers

## Frontend Rule

The visual migration rule remains unchanged:

- copy existing launcher designs and controls
- reuse WPF grouping, spacing, iconography, labels, and hierarchy
- do not redesign a page because a contract is missing
- if data is missing, add the contract or adapter, not a new visual language
- prefer existing launcher control semantics such as `MyCard`, `MyListItem`, `MyButton`, `MyIconButton`, and `MyExtraButton`

## Strategy

The migration should now move in this order.

### Phase 1: replace spike bootstrap inputs with real shell inputs

Status:

- complete

Target:

- stop creating most shell state from `SpikeSampleFactory`
- start building `LauncherFrontendShellRequest` and related requests from real runtime state

Main files involved:

- `PCL.Frontend.Spike/Desktop/App.axaml.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Construction.cs`
- `PCL.Frontend.Spike/Workflows/SpikeInputResolver.cs`
- `PCL.Core/App/Essentials/LauncherFrontendNavigationService.cs`
- `PCL.Core/App/Essentials/LauncherStartupWorkflowService.cs`
- `PCL.Core/App/Essentials/LauncherMainWindowStartupWorkflowService.cs`

Deliverable:

- a frontend-side composition layer that gathers real startup and shell state, then calls the portable backend services directly

Important constraint:

- do not move planning logic into the frontend
- only replace fixture request construction with real request construction

Delivered files:

- `PCL.Frontend.Spike/Workflows/FrontendShellCompositionService.cs`
- `PCL.Frontend.Spike/Models/FrontendShellComposition.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Construction.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Navigation.cs`
- `PCL.Core/App/Essentials/LauncherFrontendRuntimeStateService.cs`
- `PCL.Core.Backend/PCL.Core.Backend.csproj`

### Phase 2: wire prompt commands to real actions

Status:

- complete

Target:

- turn prompt rendering into actual frontend behavior

Main files involved:

- `PCL.Core/App/Essentials/LauncherFrontendPromptService.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Prompts.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceActions.cs`

Deliverable:

- `LauncherFrontendPromptCommandKind` commands dispatch into:
  - consent persistence
  - telemetry preference changes
  - launch abort/continue handling
  - crash export and log navigation
  - Java download flows
  - route changes and shell actions

Important constraint:

- the frontend should execute commands or delegate to adapters
- it should not reinterpret backend prompt policy

Delivered files:

- `PCL.Frontend.Spike/Workflows/FrontendShellActionService.cs`
- `PCL.Frontend.Spike/Workflows/FrontendRuntimePaths.cs`
- `PCL.Frontend.Spike/Desktop/App.axaml.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Prompts.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Construction.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.StaticMaps.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceInitialization.cs`

What this means in practice:

- startup prompt choices now persist `SystemEula`, `SystemTelemetry`, and prompt-driven hint settings through the same config files the runtime shell reads
- launch prompt choices now affect launch continuation/abort state and can materialize a prompt-driven Java runtime into frontend artifact directories
- crash prompt choices now route to the copied shell surfaces and can materialize/export crash artifacts through portable export services
- prompt lanes now appear only when their lifecycle actually exists:
  - startup at boot
  - launch after a launch attempt
  - crash after an explicit crash event trigger

Important caveat:

- Phase 2 is complete for prompt-command execution inside the replacement frontend boundary, but it does not make launch networking, Java transfer, or crash save-picker behavior fully live

### Phase 3: wire launch readiness and launch flow to real backend state

Status:

- complete

Target:

- replace launch-page sample labels, Java summaries, login summaries, and prompt lanes with real launch planning results

Main files involved:

- `PCL.Core/Minecraft/Launch/*`
- `PCL.Core/App/Essentials/LauncherFrontendPromptService.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Construction.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Prompts.cs`
- `PCL.Frontend.Spike/Desktop/Controls/PclLaunchLeftPanel.axaml`
- `PCL.Frontend.Spike/Desktop/Controls/PclLaunchRightPanel.axaml`

Deliverable:

- the launch page consumes real:
  - login requirement and identity state
  - launch precheck prompts
  - Java selection and download prompts
  - resolution, classpath, and prerun summaries

Delivered files:

- `PCL.Frontend.Spike/Workflows/FrontendLaunchCompositionService.cs`
- `PCL.Frontend.Spike/Models/FrontendLaunchComposition.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Construction.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.LaunchUpdate.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Prompts.cs`
- `PCL.Frontend.Spike/Workflows/FrontendShellActionService.cs`
- `PCL.Core/App/Essentials/LauncherFrontendRuntimeStateService.cs`
- `PCL.Frontend.Spike/Desktop/Controls/PclLaunchRightPanel.axaml`

What this means in practice:

- the normal `app` path now composes launch state from real config, profile, instance-manifest, and protected runtime files
- copied launch labels and summaries no longer depend on the old `_launchPlan` fixture path
- launch prompt lanes are now built from runtime-backed precheck and Java workflow state
- replay mode through `InputRoot` still exists for inspection and comparison

### Phase 4: replace route-local fixtures with backend-backed page models

Target:

- keep copied Avalonia layouts
- replace hard-coded per-page values with real data models

This work should be grouped by data source, not only by page.

#### 4A. Setup and launcher settings

Use or extend:

- configuration access
- startup and launcher app services
- Java runtime and secret/runtime services where relevant

Needed outcome:

- setup pages stop using only `FrontendShellViewModel` fixture values
- actual saved settings and runtime state become the frontend source of truth

Status:

- complete as of 2026-04-04

Delivered checkpoints:

- `5f2daad3` `feat: compose runtime-backed setup surfaces`
- `cdb115de` `feat: wire setup validation commands`
- `3dc19bcf` `feat: wire setup file management actions`
- `f4387d21` `feat: compose real setup update status`

Delivered scope:

- copied setup routes now compose real state from shared JSON config, local YAML config, protected values, launcher log/runtime folders, Java storage, and launcher metadata/update sources
- copied setup controls now persist back to the real launcher config store instead of local spike-only fields
- copied setup commands for logs, config export/import, proxy apply, Java selection, background/music assets, title-bar image, homepage tutorial generation, and update detail/download targets now execute real shell/file actions
- copied `设置/更新` cards now show real current-version and latest-version status instead of the old demo card toggle

Explicit 4A boundary:

- this slice does not port the old WPF in-place self-update patch-and-restart installer flow
- the hidden optional AquaCL card remains dormant copied UI and is not part of the completed 4A scope

#### 4B. Instance and profile pages

Use or extend:

- `MinecraftLaunchProfileStorageService`
- launcher identity and storage services
- instance metadata and profile document access

Needed outcome:

- overview, settings, export, install, world, screenshot, server, and resource pages show real instance data

Status:

- complete as of 2026-04-04

Delivered checkpoints:

- `127dac06` `feat: add runtime instance composition layer`
- `e6753070` `feat: wire runtime-backed instance shell surfaces`
- `b079434d` `fix: honor labymod instance resource folders`

Delivered scope:

- copied instance routes now compose real state from launcher folder selection, selected instance name, instance-local config, version manifests, saves, screenshots, `servers.dat`, and resource directories
- copied instance setup controls now persist back to `versions/<instance>/PCL/config.v1.yml` instead of reusing global launch settings
- copied instance overview controls now persist favorite/category/icon metadata and the content pages now enumerate real worlds, screenshots, servers, mods, disabled mods, resource packs, shaders, and schematics
- legacy `versions/<instance>/PCL/Setup.ini` fallback and migration is handled inside the instance composition/action layer
- LabyMod-specific resource-folder routing is honored when opening resource directories

Explicit 4B boundary:

- this slice completes page-model migration, not every instance-side action flow
- several buttons still intentionally dispatch placeholder adapter intents, including rename, description edit, launch-script export, dry-run/test launch, reset/delete flows, export config import/save, server creation, local file-picker installs, and profile creation
- those remaining gaps belong to later adapter/action work and should not be solved by reintroducing fixture page state

Recommended starting point:

- preserve the now-completed setup and instance families as-is
- use 4B as the reference implementation for route-local page composition from runtime files

#### 4C. Download pages

Use or add:

- backend-facing contracts for catalog lists, search filters, selection state, and install planning

Needed outcome:

- copied download pages keep their WPF-derived structure, but bind to real search/install data

#### 4D. Version-saves routes

These are still a clear gap:

- `VersionSavesInfo`
- `VersionSavesBackup`
- `VersionSavesDatapack`

Needed outcome:

- the `VersionSaves` secondary page stops being mostly contract-summary territory and gains real data contracts

### Phase 5: separate reusable frontend adapters from spike-only tooling

Status:

- complete as of 2026-04-04

Target:

- keep the spike useful as an inspection tool
- stop letting spike convenience code define the real frontend architecture

Main files involved:

- `PCL.Frontend.Spike/Workflows/*`
- `PCL.Frontend.Spike/ViewModels/*`

Deliverable:

- a clearer split between:
  - reusable frontend composition and adapter logic
  - spike-only sample generation, replay, and inspection helpers

Delivered checkpoints:

- `fb4c56c5` `refactor: isolate inspection-only spike workflows`
- `40467f7d` `refactor: add frontend inspection composition boundary`

Delivered files:

- `PCL.Frontend.Spike/Workflows/Inspection/Spike*.cs`
- `PCL.Frontend.Spike/Workflows/Inspection/FrontendInspectionShellCompositionService.cs`
- `PCL.Frontend.Spike/Workflows/Inspection/FrontendInspectionLaunchCompositionService.cs`
- `PCL.Frontend.Spike/Workflows/Inspection/FrontendInspectionCrashCompositionService.cs`
- `PCL.Frontend.Spike/Workflows/FrontendShellCompositionService.cs`
- `PCL.Frontend.Spike/Workflows/FrontendLaunchCompositionService.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Construction.cs`

What this means in practice:

- the frontend keeps replay/sample inspection support, but those paths are now explicitly contained inside `Workflows/Inspection`
- runtime shell and launch composition plus crash prompt bootstrap no longer call raw spike sample/input helpers directly
- the copied Avalonia UI remains unchanged while the adapter boundary becomes clearer for upcoming Phase 4C/4D work

## Route Status Matrix

| Area | Current visual status | Real backend source now | Main missing piece |
| --- | --- | --- | --- |
| Startup shell | portable and route-aware | `LauncherStartupWorkflowService`, `LauncherFrontendShellService` | real request construction and runtime refresh |
| Prompt queue | portable rendering and command execution exist | `LauncherFrontendPromptService` | durable side effects and non-launch route state still need broader page-model integration |
| Launch page | copied shell layout and runtime-backed composition | launch workflow services under `PCL.Core/Minecraft/Launch` plus `FrontendLaunchCompositionService` | broader launch cutover and keeping replay mode scoped to inspection |
| Setup pages | many copied layouts | real setup composition and update-status adapters now exist | 4A complete; only dormant optional update copy remains outside scope |
| Instance pages | many copied layouts | `FrontendInstanceCompositionService` plus runtime file/config composition now exist | live action adapters for some instance buttons are still placeholder-only |
| Download pages | copied install and resource layouts | navigation + page-content summary seam exists | real catalog/search/install contracts |
| Tools pages | copied help/lobby/test layouts | shell contracts exist | real tool data sources and widget actions |
| Version saves | still relatively weak | navigation seam exists | dedicated real page contracts and adapters |

## Immediate Next Work

This is the recommended concrete order for the next frontend passes.

1. Treat setup and instance routes as the completed reference implementations for route-local composition and persistence.
2. Finish download-page and `VersionSaves` data contracts without changing the copied Avalonia layouts.
3. Replace remaining placeholder instance/tool/download actions with real adapter-style shell actions where practical.
4. Preserve the runtime-backed launch route and avoid reintroducing fixture state into the normal `app` path.
5. Keep replay/inspection helpers explicit and scoped so they do not leak back into production-facing composition paths.

## Backend Support Needed

The frontend workstream should ask for backend-facing contracts when any page still needs to infer state from the old WPF layer.

The most likely contract additions are:

- download catalog and install-state models
- save management models for `VersionSaves`
- prompt command execution seams that return durable state updates
- live adapter/action seams for remaining instance, tool, and download buttons

## What Success Looks Like

This phase is complete when:

- prompt choices cause real launcher actions or persisted state changes
- the prompt inbox lifecycle matches the actual shell lifecycle instead of showing static sample lanes
- frontend prompt execution stays inside adapter-style shell code without moving policy into the frontend

Phase 2 is now in that state.

Phase 3 is now also in that state for runtime-backed launch-page composition on the normal `app` path.

Phase 4B is now in that state for runtime-backed instance-page composition and persistence, with remaining non-live button flows explicitly outside its scope.

Phase 5 is now also in that state for frontend-adapter cleanup and inspection helper separation.

The broader migration will move from “portable spike shell” to “real replacement frontend backed by portable services” when the remaining Phase 4C/4D page-model work is done.
