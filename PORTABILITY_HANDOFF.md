# PCL-CE Portability Handoff

## Executive Summary

The portability phase has crossed an important boundary:

- the portable backend is real
- the replacement frontend shell is real
- the next major task is no longer extraction alone
- the next major task is wiring real backend logic into the frontend and removing spike-owned fixture state from the critical path

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

## The Biggest Current Truth: The Frontend Is Still Mostly Fed By Spike Inputs

This is the key handoff point.

The frontend shell contracts are real, but the shell bootstrap is still mostly driven by spike-owned inputs and sample factories.

Current bootstrap path:

- `PCL.Frontend.Spike/Desktop/App.axaml.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Construction.cs`

Current request/input providers:

- `PCL.Frontend.Spike/Workflows/SpikeInputResolver.cs`
- `PCL.Frontend.Spike/Workflows/SpikeSampleFactory.cs`
- `PCL.Frontend.Spike/Workflows/SpikeHostInputFactory.cs`

Current problem:

- the shell calls real portable services
- but many of the requests and page values are still created by fixture code instead of by real runtime adapters

That means the next frontend engineer should not spend the next phase primarily making more fixture data.

They should replace fixture state with real request construction and real backend-driven page state.

## What Still Isn’t Done

### 1. Real frontend composition layer

Missing today:

- a frontend-side adapter layer that reads real launcher state
- real construction of shell, prompt, startup, launch, and page requests
- durable frontend state derived from backend results rather than sample factories

This is the most important missing layer for the frontend workstream.

### 2. Prompt command execution

Prompt rendering is portable.

Prompt action handling is still mostly spike behavior:

- command clicks often only write to the activity feed
- they do not yet consistently trigger real persistence, launch continuation, abort, export, or route logic

### 3. Page-specific production data

Many copied pages still use hard-coded view-model state even when their visuals are already good.

The main remaining data gaps are:

- setup page data binding to real config/runtime state
- instance page binding to real profile and file state
- download route binding to real search/catalog/install planning state
- `VersionSaves` subpages
- richer tool widgets

### 4. Secret/auth portability boundary

This area is improved, but still deserves caution:

- `PCL.Core/Utils/Secret/*`
- encrypted profile/config call sites

This remains one of the main backend-side risks for cutover.

### 5. WPF shell glue reduction

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

### Step 1: stop relying on `SpikeSampleFactory` for the shell bootstrap

Build a real composition layer that:

- gathers startup inputs
- gathers shell visibility inputs
- constructs `LauncherFrontendShellRequest`
- constructs real page-content and prompt inputs

Do not move workflow policy into the frontend.

### Step 2: wire prompt commands to real backend and shell actions

Use the existing `LauncherFrontendPromptCommandKind` contract and turn it into:

- route changes
- persisted consent/settings changes
- launch continue/abort decisions
- crash export/log actions
- Java download decisions

### Step 3: feed the launch page from real launch workflow services

The launch route should become the first page where the shell is backed by real runtime planning, not fixture summaries.

That means wiring:

- login state
- precheck prompts
- Java requirement state
- Java download prompts
- launch readiness summaries

### Step 4: replace route-local fixture values page family by page family

Recommended order:

1. setup pages
2. instance pages
3. download pages
4. version-saves pages
5. denser tool widgets

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

- `9007b72a` `refactor: Simplify the logic of FrontendShellViewModel and seperating it into multiple files`
- `0c92fedf` `feat: copy download resource list surfaces`
- `b1f23fdc` `feat: copy instance overview spike surface`
- `570f9ae0` `feat: copy instance export spike surface`
- `552b7de9` `feat: copy instance settings spike surface`
- `a50d5388` `feat: copy instance install spike surface`
- `36784278` `feat: copy instance content spike surfaces`
- `8c58b28b` `feat: copy instance resource spike surfaces`
- `f2754dc2` `docs: refresh frontend migration status`

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
- `PCL.Frontend.Spike/Workflows/SpikeInputResolver.cs`
- `PCL.Frontend.Spike/Workflows/SpikeSampleFactory.cs`

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
- real prompt action execution
- real page models behind the copied Avalonia UI

That is the correct continuation point for both the frontend migration plan and the broader portability effort.
