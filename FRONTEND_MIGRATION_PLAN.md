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

## Current Status

What is already in a usable migration state:

- `PCL.Core.Foundation` is a stable portable foundation layer
- `PCL.Core.Backend` is the source of truth for most startup, launch, login, Java, and crash policy
- `PCL.Core.Backend.Test` gives a regression harness for backend behavior
- `PCL.Frontend.Spike` proves non-WPF backend consumption
- `LauncherFrontendShellService` and `LauncherFrontendNavigationService` now provide a portable startup plus navigation shell seam
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
- the spike can now render a backend-driven shell/navigation transcript and execution artifact set

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
- it is still not finished enough to ignore

### Priority 3: reduce Windows leakage in `PCL.Core`

Why this matters:

- the new frontend will be easier to build if backend-facing contracts do not quietly depend on Windows-specific helpers

Needed outcome:

- adapter-only Windows helpers
- clearer separation between reusable services and platform behavior

### Priority 4: add or harden frontend-facing contracts where missing

Likely areas:

- prompt models and prompt results
- startup/bootstrap and consent interactions
- navigation/page state inputs
- profile/auth presentation models
- crash result/export interactions

Needed outcome:

- the new frontend consumes stable request/response or plan/result contracts
- frontend code does not need to reverse-engineer WPF launcher state

Current starting seam:

- startup plus navigation shell data is already available via `LauncherFrontendShellService`
- the next contracts should extend that seam into prompts, page-specific data, and profile/auth surfaces

## Two Parallel Workstreams

### Workstream A: frontend engineer

Start now.

Primary objective:

- build the replacement frontend shell against backend contracts

Recommended order:

1. consume the existing startup plus navigation shell contract
2. build app shell and route rendering on top of that contract
3. build prompt rendering abstractions
4. implement low-risk pages first
5. integrate profile/auth and launch UI after the missing contracts are filled in

Rules:

- do not copy WPF event flow as architecture
- do not rebuild launch/login/Java/crash logic in the frontend
- when a contract is missing, ask for it from the backend workstream instead of embedding policy locally

### Workstream B: backend engineer

Continue in parallel.

Primary objective:

- make the current backend and shell seams easier and safer for the new frontend to consume

Recommended order:

1. keep shrinking `Application.xaml.vb`
2. keep shrinking `FormMain.xaml.vb`
3. finish `Utils.Secret`
4. tighten Windows-only boundaries in `PCL.Core`
5. add missing frontend-facing contracts discovered by the frontend engineer
6. keep expanding the spike only where it helps prove a new seam

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

- frontend engineer builds the real startup shell and navigation skeleton from the existing portable shell contract
- backend engineer supports with any missing startup/prompt contracts

### Milestone B: contract hardening

- frontend engineer integrates a few representative flows
- backend engineer removes any remaining accidental WPF coupling those flows expose

### Milestone C: migration confidence

- `PCL.Frontend.Spike` and/or the new frontend can exercise the key workflows cleanly
- remaining work becomes UI completeness and cutover planning instead of architectural untangling

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
