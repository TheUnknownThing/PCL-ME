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
- Phase 2 is now the active frontend task

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

### Prompt actions are not yet real frontend-to-backend operations

Prompt rendering is already portable, but command handling is still mostly spike behavior:

- prompt options map to `LauncherFrontendPromptCommandKind`
- the desktop shell currently records those actions in the activity list instead of driving real workflows, persistence, or OS actions

### The next missing middle is prompt and page-state composition

The shell-level composition layer now exists.

The missing frontend layers are now:

- prompt command dispatch into backend or platform actions
- launch-state construction from real runtime/profile/config state
- route-local page models derived from backend/state sources instead of fixtures

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

- active

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

### Phase 3: wire launch readiness and launch flow to real backend state

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

#### 4B. Instance and profile pages

Use or extend:

- `MinecraftLaunchProfileStorageService`
- launcher identity and storage services
- instance metadata and profile document access

Needed outcome:

- overview, settings, export, install, world, screenshot, server, and resource pages show real instance data

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

## Route Status Matrix

| Area | Current visual status | Real backend source now | Main missing piece |
| --- | --- | --- | --- |
| Startup shell | portable and route-aware | `LauncherStartupWorkflowService`, `LauncherFrontendShellService` | real request construction and runtime refresh |
| Prompt queue | portable rendering exists | `LauncherFrontendPromptService` | real command dispatch and persistence |
| Launch page | copied shell layout | launch workflow services under `PCL.Core/Minecraft/Launch` | live state source instead of sample plan injection |
| Setup pages | many copied layouts | config and app services exist | route-specific real settings adapters |
| Download pages | copied install and resource layouts | navigation + page-content summary seam exists | real catalog/search/install contracts |
| Tools pages | copied help/lobby/test layouts | shell contracts exist | real tool data sources and widget actions |
| Instance pages | many copied layouts | profile storage and launch profile services exist | real instance/page models |
| Version saves | still relatively weak | navigation seam exists | dedicated real page contracts and adapters |

## Immediate Next Work

This is the recommended concrete order for the next frontend passes.

1. Replace prompt activity-log stubs with real handlers for consent, navigation, crash export, launch abort, and Java download actions.
2. Persist prompt-driven setting changes through the same config keys the WPF shell uses.
3. Feed the launch page from real launch workflow planning requests built from current runtime/config/profile state.
4. Introduce explicit backend-facing page models for settings, instance, and download routes that still rely on hard-coded view-model fixtures.
5. Keep the copied Avalonia layouts stable while swapping only the data source underneath them.

## Backend Support Needed

The frontend workstream should ask for backend-facing contracts when any page still needs to infer state from the old WPF layer.

The most likely contract additions are:

- richer setup page models
- richer instance page models
- download catalog and install-state models
- save management models for `VersionSaves`
- prompt command execution seams that return durable state updates

## What Success Looks Like

This phase is complete when:

- the desktop shell no longer depends mainly on `SpikeSampleFactory` for its day-to-day state
- copied Avalonia pages are driven by real backend-owned request/response models
- prompt choices cause real launcher actions or persisted state changes
- frontend code is mostly about state binding, navigation, dialogs, and OS actions
- policy remains in `PCL.Core` / `PCL.Core.Backend`

At that point the migration will have moved from “portable spike shell” to “real replacement frontend backed by portable services.”
