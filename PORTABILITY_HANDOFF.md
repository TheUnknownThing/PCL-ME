# PCL-CE Portability Handoff

## Executive Summary

The portability phase has crossed an important boundary:

- the portable backend is real
- the replacement frontend shell is real
- frontend migration Phase 1 is complete as of 2026-04-04
- frontend migration Phase 2 prompt-command execution is complete as of 2026-04-04
- frontend migration Phase 3 launch-state integration is complete as of 2026-04-04
- frontend migration Phase 4A setup-page model migration is complete as of 2026-04-04
- frontend migration Phase 4B instance-page model migration is complete as of 2026-04-04
- frontend migration Phase 5 frontend-adapter cleanup and spike separation is complete as of 2026-04-04
- the next major frontend task is Phase 4C download-page model migration

The repo is therefore in this state:

- safe to continue frontend migration now
- safe to continue backend cleanup in parallel now
- not yet ready for frontend cutover
- no longer blocked on “can a non-WPF shell exist?”

## The Repo As It Actually Exists

Use this map as the current source of truth.

### `PCL.Core.Foundation`

- portable primitives and low-level support code
- the cleanest portability layer in the repo
- no WPF ownership

### `PCL.Core`

- the main source tree
- contains portable services, adapters, configuration, UI infrastructure, and remaining Windows-specific compatibility code
- this is where the real implementation still lives

### `PCL.Core.Backend`

- a `net8.0` portable projection of files linked from `PCL.Core`
- this is not a separate backend codebase with independent source files
- it is the portable subset of the `PCL.Core` implementation packaged as a backend-facing project

Key implication:

- when you add portable backend behavior, you will often still edit files under `PCL.Core`, not under a parallel `PCL.Core.Backend` source tree

### `PCL.Frontend.Spike`

- the current replacement-frontend proving ground
- contains:
  - CLI inspection and replay flows
  - an Avalonia desktop shell
  - copied launcher-style UI for many routes
- currently the best place to wire real backend logic into a non-WPF frontend

### `Plain Craft Launcher 2`

- the legacy WPF shell
- still the visual source of truth
- still owns a lot of runtime glue and platform behavior
- should keep shrinking toward shell-only responsibilities

## What Is Already Portable And Real

### Startup and shell contracts

The frontend already has real portable seams for:

- startup workflow planning
- startup bootstrap directory and config preparation
- immediate startup shell actions
- startup consent and milestone decisions
- top-level navigation, sidebar composition, breadcrumbs, and utility surfaces
- prompt queue generation
- page-surface summary generation

Important files:

- `PCL.Core/App/Essentials/LauncherStartupWorkflowService.cs`
- `PCL.Core/App/Essentials/LauncherStartupBootstrapService.cs`
- `PCL.Core/App/Essentials/LauncherStartupShellService.cs`
- `PCL.Core/App/Essentials/LauncherMainWindowStartupWorkflowService.cs`
- `PCL.Core/App/Essentials/LauncherFrontendNavigationService.cs`
- `PCL.Core/App/Essentials/LauncherFrontendPromptService.cs`
- `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs`

### Launch, login, Java, and crash workflows

Portable services already exist for:

- launch precheck and prompt generation
- launch argument planning
- classpath and replacement-value planning
- prerun file planning
- session startup and watcher planning
- Microsoft login planning and execution
- Authlib and third-party login planning and execution
- Java runtime selection, prompting, and download planning
- Java transfer-session planning
- crash export, archive, prompt, and response workflows

Important directory:

- `PCL.Core/Minecraft/Launch`

### Secret and identity progress

The repo already has meaningful portability progress around identity and protected data:

- launcher identity resolution and runtime resolution
- legacy identity fallback behavior
- secret-key resolution and storage services
- versioned launcher data parsing
- data protection runtime routing
- Windows device identity isolation

Important files:

- `PCL.Core/App/Essentials/LauncherIdentityResolutionService.cs`
- `PCL.Core/App/Essentials/LauncherIdentityRuntimeService.cs`
- `PCL.Core/App/Essentials/LauncherLegacyIdentityService.cs`
- `PCL.Core/App/Essentials/LauncherSecretKeyResolutionService.cs`
- `PCL.Core/App/Essentials/LauncherVersionedDataService.cs`
- `PCL.Core/Utils/Secret/WindowsDeviceIdentityProvider.cs`

### Test coverage

There is now substantial regression coverage for the portable service layer.

Important test locations:

- `PCL.Core.Test/App`
- `PCL.Core.Test/Minecraft`
- `PCL.Core.Backend.Test`

## What The Avalonia Frontend Already Does Well

The current desktop shell in `PCL.Frontend.Spike` is much more than a blank shell.

### It already consumes real portable shell contracts for:

- route topology
- sidebar grouping
- utility-button visibility
- prompt queue structure
- summary page content

### It already copies the WPF launcher visually for many routes

Copied or near-copied surfaces currently exist for:

- launch shell framing
- top chrome and back-title behavior
- grouped non-launch sidebar navigation
- `设置/关于`
- `设置/反馈`
- `设置/日志`
- `设置/更新`
- `设置/游戏联机`
- `设置/游戏管理`
- `设置/启动器杂项`
- `设置/Java`
- `设置/界面`
- `下载/自动安装`
- download resource list routes
- `工具/联机大厅`
- `工具/帮助`
- `工具/测试`
- `实例/概览`
- `实例/设置`
- `实例/导出`
- `实例/安装`
- `实例/世界`
- `实例/截图`
- `实例/服务器`
- `实例/Mod`
- `实例/已禁用 Mod`
- `实例/资源包`
- `实例/光影包`
- `实例/投影原理图`

The main UI work lives in:

- `PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`
- `PCL.Frontend.Spike/Desktop/Controls/PclLaunchLeftPanel.axaml`
- `PCL.Frontend.Spike/Desktop/Controls/PclLaunchRightPanel.axaml`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.*.cs`

## Frontend Migration Status

### Phase 1 is done

The original Phase 1 target was:

- replace spike bootstrap inputs with real shell inputs
- stop creating most shell state from `SpikeSampleFactory`
- start building `LauncherFrontendShellRequest` and related requests from real runtime state

That work is now implemented.

Important files:

- `PCL.Frontend.Spike/Workflows/FrontendShellCompositionService.cs`
- `PCL.Frontend.Spike/Models/FrontendShellComposition.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Construction.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Navigation.cs`
- `PCL.Core/App/Essentials/LauncherFrontendRuntimeStateService.cs`
- `PCL.Core.Backend/PCL.Core.Backend.csproj`

Important commits:

- `4ef0a3f9` `Add runtime shell composition layer`
- `0447d15d` `Use runtime shell composition in frontend bootstrap`
- `aff946ea` `Expose frontend runtime state in backend slice`
- `e23deb6c` `Use backend runtime state in shell composition`

What this means in practice:

- shell startup requests now come from real launcher paths and persisted config files
- consent and version-isolation migration inputs now mirror the WPF startup construction path
- shell utility visibility now has real runtime state for game logs and running tasks
- encrypted startup count is now read through a backend-facing runtime service instead of being hard-coded
- replay inputs via `InputRoot` still work for inspection scenarios

### Phase 3 is done

The original Phase 3 target was:

- replace launch-page sample labels, Java summaries, login summaries, and prompt lanes with real launch planning results
- source launch readiness from runtime profile/config/instance state
- keep the copied launch page layouts intact while swapping the state source underneath them

That work is now implemented for the `app` path when the frontend is not running from replay inputs.

Important files:

- `PCL.Frontend.Spike/Workflows/FrontendLaunchCompositionService.cs`
- `PCL.Frontend.Spike/Models/FrontendLaunchComposition.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Construction.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.LaunchUpdate.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Prompts.cs`
- `PCL.Frontend.Spike/Workflows/FrontendShellActionService.cs`
- `PCL.Core/App/Essentials/LauncherFrontendRuntimeStateService.cs`
- `PCL.Frontend.Spike/Desktop/Controls/PclLaunchRightPanel.axaml`
- `PCL.Frontend.Spike/Desktop/Controls/PclLaunchButton.axaml`
- `PCL.Frontend.Spike/Desktop/Controls/PclButton.axaml`

What this means in practice:

- the launch page now reads selected instance, launcher folder, selected profile, Java state, and launch count from real runtime files
- launch precheck prompts are now built from runtime-backed launch requests instead of the old launch fixture path
- launch summary data now includes real resolution, classpath, replacement-value, and prerun planning state
- copied launch-pane labels now reflect runtime-composed state instead of placeholder snapshot strings
- the copied left launch controls are interactive again after the custom-control hit-testing fix

Important caveat:

- `app --input-root ...` still intentionally uses replay-backed launch inputs for inspection scenarios

### The biggest current truth

The frontend shell bootstrap and launch route are no longer mostly fixture-driven.

The remaining spike-heavy areas are now:

- crash/launch replay plan injection used by inspection flows
- route-local page fixtures outside the completed launch/setup/instance families
- spike-only tooling that still sits beside reusable frontend adapters

## What Still Isn’t Done

### 1. Page-specific production data

Many copied pages still use hard-coded view-model state even when their visuals are already good.

The main remaining data gaps are:

- download route binding to real search/catalog/install planning state
- `VersionSaves` subpages
- richer tool widgets

### 2. Launch cutover beyond composition

Phase 3 made the launch route runtime-backed inside the replacement frontend boundary.

The remaining launch-side work is now outside that composition step:

- full launcher cutover beyond frontend-side composition and prompt handling
- replacing replay/sample launch execution paths where the spike still uses them intentionally
- continuing to separate reusable launch adapters from inspection-only helpers

### 3. Secret/auth portability boundary

This area is improved, but still deserves caution:

- `PCL.Core/Utils/Secret/*`
- encrypted profile/config call sites

This remains one of the main backend-side risks for cutover.

### 4. WPF shell glue reduction

The WPF shell is much thinner than before, but the final cutover still depends on continuing to reduce and isolate:

- startup glue
- prompt wiring
- route and page thread coordination
- windowing and platform event glue

Important files:

- `Plain Craft Launcher 2/Application.xaml.vb`
- `Plain Craft Launcher 2/FormMain.xaml.vb`

## Recommended Frontend Workstream

This is the recommended order for the next frontend engineer.

### Step 1: Phase 1 is complete, use the existing shell composition layer

Start from:

- `PCL.Frontend.Spike/Workflows/FrontendShellCompositionService.cs`
- `PCL.Core/App/Essentials/LauncherFrontendRuntimeStateService.cs`

Do not replace this with new fixtures.

### Step 2: Phase 2 is complete, preserve the new prompt lifecycle and command adapters

The prompt system now does the Phase 2 job:

- startup prompts persist consent and telemetry decisions
- launch prompts are created only from a launch attempt
- crash prompts are created only from an explicit crash event trigger
- crash export/log actions and Java materialization actions execute through frontend adapters

Important files:

- `PCL.Frontend.Spike/Workflows/FrontendShellActionService.cs`
- `PCL.Frontend.Spike/Workflows/FrontendRuntimePaths.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Prompts.cs`

Important caveat:

- Phase 2 does not make launch or crash workflows fully live; it makes prompt-command execution real inside the replacement shell boundary

### Step 3: Phase 3 is complete, preserve the runtime launch composition path

The launch route now reads runtime-backed launch state through the dedicated composition layer.

Start from:

- `PCL.Frontend.Spike/Workflows/FrontendLaunchCompositionService.cs`
- `PCL.Frontend.Spike/Models/FrontendLaunchComposition.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.LaunchUpdate.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Prompts.cs`

Do not regress this by reintroducing launch-route fixtures for the normal `app` path.

Important caveat:

- replay mode through `InputRoot` is still intentionally sample-backed
### Step 4: replace route-local fixture values page family by page family

Current state:

1. setup pages are complete as Phase 4A
2. instance pages are complete as Phase 4B
3. download pages and version-saves pages remain unfinished Phase 4 slices
4. adapter cleanup and spike separation is complete as Phase 5

Phase 4A delivered:

- real setup-page composition through:
  - `PCL.Frontend.Spike/Models/FrontendSetupComposition.cs`
  - `PCL.Frontend.Spike/Workflows/FrontendSetupCompositionService.cs`
  - `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SetupComposition.cs`
- real setup-page persistence and shell-side actions through:
  - `PCL.Frontend.Spike/Workflows/FrontendShellActionService.cs`
  - `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceActions.cs`
- runtime-backed setup/update status through:
  - `PCL.Frontend.Spike/Models/FrontendSetupUpdateStatus.cs`
  - `PCL.Frontend.Spike/Workflows/FrontendSetupUpdateStatusService.cs`
  - `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.LaunchUpdate.cs`

What this means in practice:

- copied setup surfaces now read and persist real shared, local, and protected launcher configuration
- copied setup commands for logs, config export/import, proxy apply, background/music asset folders, title-bar image, homepage tutorial file, and update detail/download targets now perform real shell/file actions
- copied `设置/更新` cards now resolve current-version metadata and live latest-version status from the existing update-source family instead of fixture toggles

Important caveat for the next engineer:

- the hidden optional AquaCL card remains dormant copied UI and is not part of the completed 4A setup slice
- the WPF-style in-place self-update patch-and-restart installer flow has not been ported into the Avalonia shell; 4A stops at real status, changelog, and download-target composition

Phase 4B delivered:

- real instance-page composition through:
  - `PCL.Frontend.Spike/Models/FrontendInstanceComposition.cs`
  - `PCL.Frontend.Spike/Workflows/FrontendInstanceCompositionService.cs`
  - `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.InstanceComposition.cs`
- runtime-backed instance page state and persistence through:
  - `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.InstanceOverview.cs`
  - `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.InstanceSetup.cs`
  - `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.InstanceExport.cs`
  - `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.InstanceInstall.cs`
  - `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.InstanceContent.cs`
  - `PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`
- instance persistence and config migration through:
  - `PCL.Frontend.Spike/Workflows/FrontendShellActionService.cs`

What this means in practice:

- copied `实例/概览`, `实例/设置`, `实例/导出`, `实例/安装`, `实例/世界`, `实例/截图`, `实例/服务器`, `实例/Mod`, `实例/已禁用 Mod`, `实例/资源包`, `实例/光影包`, and `实例/投影原理图` now compose their state from real launcher files and instance config
- the instance setup page now persists per-instance values to `versions/<instance>/PCL/config.v1.yml` instead of reusing global launch settings
- the overview favorite/category/icon selectors now persist instance metadata and the content pages now enumerate real saves, screenshots, servers, and resource folders
- legacy `versions/<instance>/PCL/Setup.ini` is migrated forward when instance config is missing
- LabyMod-specific resource folders are respected when opening resource directories

Important caveat for the next engineer:

- 4B is complete as a page-model migration milestone, not as a full production action cutover
- several instance-page buttons still intentionally route to placeholder activity/intention handlers, such as rename, description edit, script export, dry-run/test launch, delete/reset flows, export config import/save, add-server, file-picker installs, and profile creation
- treat those remaining gaps as adapter/action follow-up work, not as a reason to reintroduce fixture page state

Recommended order:

1. setup pages
2. instance pages
3. download pages
4. version-saves pages
5. denser tool widgets

Phase 5 delivered:

- inspection-only sample, replay, and execution helpers now live under:
  - `PCL.Frontend.Spike/Workflows/Inspection/*`
- runtime-facing frontend composition now reaches inspection behavior through dedicated inspection composition services:
  - `PCL.Frontend.Spike/Workflows/Inspection/FrontendInspectionShellCompositionService.cs`
  - `PCL.Frontend.Spike/Workflows/Inspection/FrontendInspectionLaunchCompositionService.cs`
  - `PCL.Frontend.Spike/Workflows/Inspection/FrontendInspectionCrashCompositionService.cs`
- reusable frontend composition and view-model construction no longer call raw spike factories or input resolvers directly on the normal app path

What this means in practice:

- replay and host-environment inspection flows remain available, but are explicitly scoped to the inspection workflow area
- runtime shell, launch, and crash composition now depend on a narrower frontend-side adapter boundary instead of sample/replay helper internals
- setup and instance page-model migration work can continue without re-entangling inspection tooling with the main frontend architecture

Recommended next order from this checkpoint:

1. download pages
2. version-saves pages
3. denser tool widgets
4. remaining adapter/action follow-up on placeholder instance/tool/download buttons

### Step 5: preserve the copied UI work

When wiring data:

- keep the copied Avalonia layouts
- replace the data source underneath them
- do not redesign pages during data integration

## Recommended Backend Workstream

The backend engineer should focus on making the frontend integration easier and less guess-based.

### Provide or extend contracts for:

- richer setup page data
- richer instance page data
- download catalog and install state
- version-saves data
- prompt command execution and persistence seams

### Continue cleanup in:

- `PCL.Core/Utils/Secret`
- Windows-only leakage inside `PCL.Core`
- the remaining WPF shell glue in `Plain Craft Launcher 2`

## Recent Checkpoint Commits

These are the most relevant recent frontend checkpoints:

- `4ef0a3f9` `Add runtime shell composition layer`
- `0447d15d` `Use runtime shell composition in frontend bootstrap`
- `aff946ea` `Expose frontend runtime state in backend slice`
- `e23deb6c` `Use backend runtime state in shell composition`
- `b1f23fdc` `feat: copy instance overview spike surface`
- `570f9ae0` `feat: copy instance export spike surface`
- `552b7de9` `feat: copy instance settings spike surface`
- `a50d5388` `feat: copy instance install spike surface`
- `36784278` `feat: copy instance content spike surfaces`
- `8c58b28b` `feat: copy instance resource spike surfaces`
- `f2754dc2` `docs: refresh frontend migration status`
- `805b0a62` `refactor: add frontend shell action adapters`
- `6624be02` `feat: execute frontend prompt commands`
- `c2c139ed` `fix: auto-open startup prompt inbox`
- `5caaa14e` `fix: scope prompt lanes to runtime events`
- `568df5d1` `Compose runtime launch state for frontend phase 3`
- `5ab8497e` `Bind copied launch pane text to runtime state`
- `b009b0f7` `Fix copied launch controls hit testing`
- `5f2daad3` `feat: compose runtime-backed setup surfaces`
- `cdb115de` `feat: wire setup validation commands`
- `3dc19bcf` `feat: wire setup file management actions`
- `f4387d21` `feat: compose real setup update status`
- `127dac06` `feat: add runtime instance composition layer`
- `e6753070` `feat: wire runtime-backed instance shell surfaces`
- `b079434d` `fix: honor labymod instance resource folders`
- `fb4c56c5` `refactor: isolate inspection-only spike workflows`
- `40467f7d` `refactor: add frontend inspection composition boundary`

## Files To Read First

If you are picking this up cold, read these first:

### Architecture and shell seams

- `PCL.Core/App/Essentials/LauncherFrontendNavigationService.cs`
- `PCL.Core/App/Essentials/LauncherFrontendPromptService.cs`
- `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs`
- `PCL.Core/App/Essentials/LauncherStartupWorkflowService.cs`

### Frontend bootstrap and state

- `PCL.Frontend.Spike/Desktop/App.axaml.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Construction.cs`
- `PCL.Frontend.Spike/Workflows/FrontendShellCompositionService.cs`
- `PCL.Frontend.Spike/Workflows/FrontendLaunchCompositionService.cs`
- `PCL.Frontend.Spike/Workflows/FrontendInstanceCompositionService.cs`
- `PCL.Core/App/Essentials/LauncherFrontendRuntimeStateService.cs`

### Real UI reference

- `PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`
- `Plain Craft Launcher 2/Pages/*`

### Backend coverage

- `PCL.Core.Test/App/*`
- `PCL.Core.Test/Minecraft/*`
- `PCL.Core.Backend.Test/*`

## Rules For The Next Engineer

- copy the existing launcher UI; do not redesign it
- do not move policy out of backend services into the frontend
- treat `PCL.Core.Backend` as a projection of `PCL.Core`, not a second independent implementation tree
- when a copied page still shows fixture data, replace the data source before changing the visuals
- when a page needs data the backend does not expose yet, add the contract instead of rebuilding WPF logic in the frontend

## Bottom Line

The repo is already past the “portability spike” stage.

The next milestone is:

- real backend-driven frontend state
- real page models behind the copied Avalonia UI
- continued removal of route-local fixtures outside the already-migrated launch route and completed setup family

That is the correct continuation point for both the frontend migration plan and the broader portability effort.
