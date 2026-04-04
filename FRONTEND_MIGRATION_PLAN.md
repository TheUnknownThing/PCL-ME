# Frontend Migration Plan

## Goal

Start the frontend migration now without waiting for a perfect backend, while keeping the replacement frontend contract-first and backend-driven.

The intended end state is:

- `PCL.Core.Backend` owns portable workflow and policy
- `PCL.Core` owns adapter-level compatibility helpers only
- `Plain Craft Launcher 2` remains the legacy WPF shell until cutover
- the new frontend owns UI composition, prompt rendering, navigation, and OS actions only

## Current Decision

The project is ready to begin frontend migration work.

That does **not** mean:

- immediate frontend cutover
- backend cleanup is finished
- the new frontend should rebuild launcher logic from scratch

It **does** mean:

- the frontend engineer can start now
- the backend engineer should continue shell and portability cleanup in parallel
- both workstreams should meet at explicit contracts, not at copied WPF behavior

## Frontend Rule

For this migration phase, the frontend should copy the existing launcher UI rather than redesign it.

That means:

- reuse current page structure, grouping, iconography, spacing, and control hierarchy wherever possible
- prefer Avalonia controls that mirror `MyCard`, `MyListItem`, `MyButton`, `MyIconButton`, and `MyExtraButton`
- do not introduce new shell-specific visual patterns when an existing launcher pattern already exists
- when the backend seam is too abstract for a page, request a better contract instead of inventing a new layout language

## Current Status

What is already in a usable migration state:

- `PCL.Core.Foundation` is a stable portable foundation layer
- `PCL.Core.Backend` is the source of truth for most startup, launch, login, Java, and crash policy
- `PCL.Core.Backend.Test` gives a regression harness for backend behavior
- `PCL.Frontend.Spike` proves non-WPF backend consumption
- `LauncherFrontendShellService` and `LauncherFrontendNavigationService` now provide a portable startup plus navigation shell seam
- `LauncherFrontendPromptService` now provides portable startup, launch, Java-download, and crash prompt contracts
- `PCL.Frontend.Spike` now also includes an Avalonia desktop shell spike that renders those portable contracts as a real UI
- the Avalonia spike now has route-aware shell composition instead of rendering only the launch page
- the Avalonia spike now copies launcher-style grouped left navigation for non-launch routes
- the Avalonia spike now uses closer launcher-style chrome for top-level and secondary routes
- the Avalonia spike now also has copied non-launch right panes for `设置/关于`, `设置/反馈`, `设置/日志`, `工具/帮助`, `设置/更新`, `设置/游戏联机`, `设置/游戏管理`, `设置/启动器杂项`, `设置/Java`, and `设置/界面`
- the Avalonia spike now also has a copied `下载/自动安装` surface instead of the old generic summary card layout
- the Avalonia spike now also has a copied `工具/联机大厅` two-column detail surface instead of the earlier simplified single-column summary
- the Avalonia spike now also pushes `工具/测试` closer to the original WPF page structure, wording, preview panels, and tool button behavior
- those copied routes now also have page-specific frontend page-content seams instead of only the generic summary contract
- `ModLaunch.vb` is now a thin launch coordinator
- `ModJava.vb` is effectively a thin adapter for this phase
- `ModCrash.vb` is now just the crash analyzer entry point
- startup and main window responsibilities have already been heavily split into shell modules

## What Was Recently Finished

Recent backend/shell milestones:

- launch execution, prerun, argument, session, profile, precheck, Java workflow, natives, and login logic were extracted from `ModLaunch.vb`
- Java preference selection and loader-related adapter work were extracted from `ModJava.vb`
- crash collection, export, prepare, analyze, and result logic were split away from `ModCrash.vb`
- profile persistence is now routed through `MinecraftLaunchProfileStorageService`
- launcher identity resolution and Windows device identity access are now more clearly separated
- secret key resolution and versioned secret-envelope parsing now live in dedicated services
- shell/runtime secret access now routes through app-layer runtime services
- the spike can now render a backend-driven shell/navigation/prompt transcript and execution artifact set
- the spike can now boot a real Avalonia shell from those same contracts
- the spike now also mirrors more of the current launcher frame by reusing copied chrome, launch layout, and grouped left-nav patterns

Recent checkpoint commits:

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
- `67f2665e` `refactor: isolate windows device identity adapter`
- `6e966b19` `refactor: extract legacy secret derivation service`
- `dfdd48d2` `refactor: extract secret key resolution services`
- `f3520e89` `feat: add frontend prompt and page surface contracts`
- `b9786c5c` `test: cover secret portability services`
- `6d4b7ff3` `feat: expose frontend shell artifacts in spike`
- `262bbb11` `refactor: extract launcher data protection service`
- `34746e39` `test: cover data protection storage seams`
- `4e01bcb7` `feat: add avalonia frontend shell spike`
- `408d8a4c` `refactor: route shell secret access through app runtime`
- `2674a878` `refactor: extract identity and legacy secret runtime services`
- `3ed64a54` `refactor: move encryption runtime into app layer`
- `8859224b` `feat: add route-aware frontend shell panels`
- `b2d7c574` `feat: align spike chrome with launcher routes`
- `cb7cdab0` `feat: copy launcher-style sidebar navigation`
- `dc20914d` `feat: simplify spike content cards`
- `9231a0be` `feat: copy setup and help shell surfaces`
- `7aea529a` `feat: add page-specific frontend content seams`
- `7b2fb304` `feat: copy setup update surface`
- `7cfa89db` `feat: copy setup game link surface`
- `62c32bf7` `feat: copy setup game manage surface`
- `4c81c806` `feat: copy setup launcher misc surface`
- `e9f2979d` `feat: copy setup java and ui surfaces`
- `9baa892a` `feat: align download install spike surface`
- `9a0ab0e5` `feat: copy tools game link detail surface`

## Current Frontend Spike State

The current Avalonia spike now proves more than startup plumbing:

- launch route is rendered as a copied left/right launcher surface rather than a generic shell placeholder
- non-launch routes now switch through a route-aware shell body
- non-launch left panes now follow the grouped `MyListItem` navigation pattern from the current launcher
- top chrome now switches between top tabs and inner-route back-title mode
- `设置/关于`, `设置/反馈`, `设置/日志`, `工具/帮助`, `设置/更新`, `设置/游戏联机`, `设置/游戏管理`, `设置/启动器杂项`, `设置/Java`, and `设置/界面` now render copied page-specific right panes rather than the generic shell summary panel
- `下载/自动安装` now renders a copied card stack with original warning strips and loader-selection hierarchy instead of the previous placeholder summary
- `工具/联机大厅` now renders copied join/create/detail/member areas instead of the earlier simplified summary-only panel
- `工具/测试` now follows the original card order and more of the original wording, form layout, and preview behavior

What still needs frontend parity work:

- many non-launch right panes are still summary-card approximations, not copied page-specific layouts yet
- setup still needs copied parity for `启动` and any denser route sections that still collapse into generic summary cards
- download now has a copied `自动安装` route, but still needs the easier resource-list routes after it
- tools still needs parity beyond the copied help, lobby, and test pages, especially the embedded server-query widget and any denser follow-up tool surfaces
- page-specific controls such as search boxes, list blocks, person/about rows, richer settings cards, and embedded custom widgets still need direct migration
- some current Avalonia controls are faithful first passes, but should continue to converge toward the original WPF behavior and spacing

## Remaining Work Before Full Frontend Cutover

### Priority 1: finish startup and window shell cleanup

Files:

- `Plain Craft Launcher 2/Application.xaml.vb`
- `Plain Craft Launcher 2/FormMain.xaml.vb`

Why this matters:

- these files still define a lot of how the current launcher boots, presents prompts, wires lifecycle state, and coordinates page routing
- a new frontend can start before this is perfect, but final cutover gets much safer once these seams are cleaner

Needed outcome:

- lifecycle and startup wiring become clearer adapter code
- page routing and prompt coordination are explicit enough to mirror in another frontend
- WPF-specific event glue is no longer mixed with reusable behavior

### Priority 2: finish the secret portability boundary

Area:

- `PCL.Core/Utils/Secret/*`
- encrypted profile/config call sites

Why this matters:

- this is still the biggest backend-side portability risk
- a new frontend should not have to guess how device identity, secret storage, and auth persistence are supposed to work

Needed outcome:

- a headless secret/auth boundary
- no hidden Windows-only assumptions for profile/config secret handling

Progress note:

- this track is healthier than before because device identity access and launcher identity resolution are now separated
- core secret-key and versioned-envelope logic are now in dedicated services with tests
- runtime ownership has also moved upward into app-layer services, which is a better fit for a future frontend adapter boundary
- it is still not finished enough to ignore

### Priority 3: reduce Windows leakage in `PCL.Core`

Why this matters:

- the new frontend will be easier to build if backend-facing contracts do not quietly depend on Windows-specific helpers

Needed outcome:

- adapter-only Windows helpers
- clearer separation between reusable services and platform behavior

### Priority 4: add or harden frontend-facing contracts where missing

Likely areas:

- startup/bootstrap and consent interactions
- navigation/page state inputs
- profile/auth presentation models
- crash result/export interactions
- page-specific surface contracts
- richer right-pane page models so copied layouts can render real content without falling back to generic summaries
- copied settings/download/tools routes that still need denser list/search/update/install data than the current seam exposes

Needed outcome:

- the new frontend consumes stable request/response or plan/result contracts
- frontend code does not need to reverse-engineer WPF launcher state

Current starting seam:

- startup plus navigation shell data is already available via `LauncherFrontendShellService`
- prompt queue data is already available via `LauncherFrontendPromptService`
- a real desktop shell scaffold already exists in the Avalonia spike
- a first page-specific seam now exists for copied settings/tools routes in `LauncherFrontendPageContentService`
- the next contracts should extend those seams into richer update/install/profile/auth surfaces

## Two Parallel Workstreams

### Workstream A: frontend engineer

Start now.

Primary objective:

- build the replacement frontend shell against backend contracts

Recommended order:

1. consume the existing startup plus navigation shell contract
2. consume the portable prompt contract for startup, launch, and crash prompts
3. build on the existing Avalonia shell spike instead of starting from zero
4. keep the copied `设置/关于`, `设置/反馈`, `设置/日志`, `工具/帮助`, `设置/更新`, `设置/游戏联机`, `设置/游戏管理`, `设置/启动器杂项`, `设置/Java`, and `设置/界面` pages stable as the reference pattern for right-pane migration
5. keep the copied `下载/自动安装`, `工具/联机大厅`, and `工具/测试` surfaces stable while replacing the next generic route surfaces with copied WPF page structures
6. continue with the easier download resource-list pages and any remaining tool surfaces that still fall back to generic summaries
7. integrate profile/auth and launch UI after the missing contracts are filled in

Rules:

- do not copy WPF event flow as architecture
- do not rebuild launch/login/Java/crash logic in the frontend
- when a contract is missing, ask for it from the backend workstream instead of embedding policy locally
- copy existing designs and controls instead of redesigning pages based on inferred intent
- if a page still looks generic, treat that as a migration gap and pull it closer to the current launcher

### Workstream B: backend engineer

Continue in parallel.

Primary objective:

- make the current backend and shell seams easier and safer for the new frontend to consume

Recommended order:

1. finish `Utils.Secret`
2. tighten Windows-only boundaries in `PCL.Core`
3. add missing frontend-facing contracts discovered by the frontend engineer
4. keep expanding the spike only where it helps prove a new seam
5. only after that, return to any remaining WPF shell cleanup worth extracting

Rules:

- keep portable policy in `PCL.Core.Backend`
- keep WPF/frontend-only behavior in the shell layer
- avoid regressing giant launcher modules back into mixed policy files

## What The New Frontend Should Not Rebuild

The replacement frontend should not own:

- launch policy
- login protocol logic
- Java selection/download policy
- crash analysis or export planning
- startup workflow planning
- version-transition policy
- milestone/update-log policy

If any of these are still frontend-owned when the migration nears cutover, the backend boundary is not finished enough.

## Practical Readiness Checklist

We are ready to start the migration if these statements are true:

- `PCL.Core.Backend` is already the source of truth for the important workflows
- `PCL.Frontend.Spike` proves at least startup-scale non-WPF consumption
- the frontend engineer can work against explicit contracts rather than WPF internals
- the backend engineer is available to keep improving the remaining seams

We are ready for full cutover only when these additional statements are true:

- `Application.xaml.vb` and `FormMain.xaml.vb` are mostly shell-only
- `Utils.Secret` no longer blocks headless consumption
- `PCL.Core` Windows-only helpers are clearly adapter-owned
- the new frontend has exercised the important flows without borrowing WPF behavior

## Recommended Near-Term Milestones

### Milestone A: frontend kickoff

- frontend engineer builds forward from the existing Avalonia shell, startup shell, navigation skeleton, and prompt rendering contracts
- frontend engineer keeps replacing generic spike scaffolding with copied launcher layouts and controls
- backend engineer supports with any missing startup/prompt contracts

### Milestone B: contract hardening

- frontend engineer integrates a few representative flows
- backend engineer removes any remaining accidental WPF coupling those flows expose

### Milestone C: migration confidence

- `PCL.Frontend.Spike` and/or the new frontend can exercise the key workflows cleanly
- remaining work becomes UI completeness and cutover planning instead of architectural untangling

## When You Can Expect A Real Frontend

As of April 3, 2026, if the engineers keep moving at roughly the current pace:

- you can already see the beginning of a real frontend shell now via the Avalonia spike
- a more convincing "real frontend" with actual startup, navigation, and prompt handling feels like the next 1 to 2 focused frontend iterations
- a frontend that is meaningfully usable for broader day-to-day flows will still take longer, because page-level surfaces and some backend seams are not finished yet

The short version is:

- first real frontend shell: now, in spike form
- full replacement frontend: not next turn

## Validation

Use this as the standard validation loop:

```bash
dotnet build PCL.Core.Backend/PCL.Core.Backend.csproj -c Debug
dotnet test PCL.Core.Backend.Test/PCL.Core.Backend.Test.csproj -c Debug
dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- startup
dotnet build PCL.Core/PCL.Core.csproj -c Debug
dotnet build "Plain Craft Launcher 2/Plain Craft Launcher 2.vbproj" -c Debug
```

## Short Advice

Treat this as a parallel migration, not a serialized one.

The right approach is:

1. start the new frontend now
2. keep the backend engineer tightening the remaining seams
3. make missing contracts explicit as soon as they are discovered
4. cut over only after the remaining startup/window and secret portability risks are materially reduced
