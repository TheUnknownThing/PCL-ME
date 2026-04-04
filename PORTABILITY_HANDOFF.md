# PCL-CE Portability Handoff

## Executive Summary

Status as of 2026-04-04:

- the portable backend is real
- the Avalonia replacement shell is real
- copied launcher UI exists for most major routes in `PCL.Frontend.Spike`
- startup, prompt, launch, setup, instance, download, and version-saves composition are now partially or mostly runtime-backed
- the tools route family now has a dedicated runtime composition path
- the repo is past the “can this work outside WPF?” stage
- the new goal is a fully working multi-platform PCL-CE launcher, not a longer-lived spike

The important change in direction is this:

- future work should be planned as small, user-verifiable launcher slices
- each slice should end in something a reviewer can click through and test in the running shell
- frontend work should keep copying the existing launcher, not redesign it

## Final Goal

The final target is:

- a fully working PCL-CE launcher frontend that can replace the legacy WPF shell
- backed by portable services and explicit OS adapters
- able to run on Windows, macOS, and Linux
- while preserving the current launcher’s page structure, controls, labels, and interaction model as closely as practical

In concrete terms, “done” means:

- the Avalonia shell can boot, navigate, configure, download, launch, diagnose, and manage instances with real behavior
- the remaining WPF-specific logic is either deleted or reduced to platform-specific adapter implementations
- cross-platform differences are isolated behind backend or shell action services instead of page-local UI code
- reviewers can validate real launcher behavior from the desktop app without depending on replay-only spike paths

## Current Repo Map

### `PCL.Core.Foundation`

- portable primitives and low-level support
- no WPF ownership

### `PCL.Core`

- main implementation tree
- contains portable services plus remaining Windows-specific compatibility code
- most new backend work still lands here

### `PCL.Core.Backend`

- portable linked projection of backend-safe code from `PCL.Core`
- not a separate implementation tree

### `PCL.Frontend.Spike`

- current replacement frontend
- contains:
  - Avalonia desktop shell
  - CLI inspection/replay tooling
  - copied launcher UI
  - frontend-side composition and shell action adapters

### `Plain Craft Launcher 2`

- legacy WPF frontend
- still the visual and behavioral source of truth
- should keep shrinking as parity moves into the portable frontend

## What Is Already In Good Shape

These areas are no longer the primary risk:

- startup workflow planning
- shell navigation contracts
- prompt rendering and prompt command execution
- launch readiness composition
- setup page runtime composition and persistence
- instance page runtime composition and persistence
- download page runtime composition
- version-saves runtime composition
- tools route runtime composition
- frontend adapter cleanup between runtime composition and inspection-only spike helpers

Important frontend-side files:

- `PCL.Frontend.Spike/Workflows/FrontendShellCompositionService.cs`
- `PCL.Frontend.Spike/Workflows/FrontendLaunchCompositionService.cs`
- `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs`
- `PCL.Frontend.Spike/Workflows/FrontendToolsCompositionService.cs`
- `PCL.Frontend.Spike/Workflows/FrontendVersionSavesCompositionService.cs`
- `PCL.Frontend.Spike/Workflows/FrontendShellActionService.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.*.cs`
- `PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`

Important backend-side directories:

- `PCL.Core/App/Essentials`
- `PCL.Core/Minecraft/Launch`
- `PCL.Core.Test`
- `PCL.Core.Backend.Test`

## What Still Separates The Project From A Real Cutover

The main remaining gaps are no longer “UI exists” gaps. They are “real behavior and cross-platform replacement” gaps.

### 1. Launch execution is not yet end-to-end replacement-shell ready

The frontend can compose real launch state, but cutover still needs:

- full replacement-shell launch execution parity
- stronger session/process integration
- fewer frontend-side placeholder artifact materializations

### 2. Several tool and utility actions are still placeholders

Examples:

- some test/tool buttons still only log intent text even though the route data is now runtime-backed
- some instance actions still stop at activity feed output
- some export and maintenance flows still need real adapters

### 3. Cross-platform adapter coverage is incomplete

The new shell must explicitly own:

- file picker behavior
- folder opening behavior
- URL opening behavior
- shortcut creation strategy
- protected storage behavior
- path conventions
- launcher packaging/update behavior

This is the area most likely to diverge across Windows, macOS, and Linux.

### 4. WPF still owns part of the real launcher behavior

The remaining work is not just frontend polish. It is a transfer of runtime responsibilities away from WPF page logic.

## Working Rules For Future Frontend Engineers

These rules should be treated as mandatory.

### Visual rule

- copy the existing launcher UI
- reuse the existing grouping, spacing, icons, labels, and control semantics
- do not redesign a page because a backend contract is missing

### Architecture rule

- policy belongs in backend services
- the frontend may compose data, route, bind controls, and call shell/OS adapters
- the frontend should not reimplement launcher policy from WPF event code

### Verification rule

Every meaningful frontend checkpoint should end with:

- a successful build
- a short list of manual steps a reviewer can perform in the running shell
- behavior that is visible in real files, folders, config, or launcher state

### Scope rule

- prefer one route family or one behavior family per checkpoint
- avoid giant “parity” batches that are hard to inspect

## Recommended Migration Plan

The active frontend plan is now organized around small, testable slices rather than one large phase bucket.

### Step 0. Preserve the current baseline

Goal:

- keep the current replacement shell buildable and navigable while further work lands

Done when:

- `dotnet build PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` passes
- the shell opens and major route groups still render

Manual verification:

- open the app
- navigate launch, setup, download, tools, instance, and version-saves routes
- confirm the copied layouts still render and route changes still work

### Step 1. Finish route-model parity

Goal:

- every major right-pane route should be backed by a route-local runtime composition model

Priority targets:

- remaining tool pages
- remaining instance action surfaces
- any download/detail subroutes still using placeholder-only state

Done when:

- route-local placeholder collections are removed or clearly isolated
- each major route family has a dedicated composition path

Status on 2026-04-04:

- tools route family: mostly done for Track 1
- remaining gap: instance detail/action surfaces still include placeholder-only commands and some sample-only detail content
- do not mark Track 1 complete until those instance/detail leftovers are removed or isolated

Manual verification:

- use real launcher files/config and confirm each route shows live data
- change the source files, refresh the route, and confirm the view updates

### Step 2. Finish shell action parity

Goal:

- replace remaining activity-feed-only actions with real shell behavior where practical

Recommended starting point for the next Track 2 engineer:

- begin with the migrated tools pages because their route-local composition is now in place
- recent frontend checkpoints for this handoff:
  - `e96247f6` `feat: runtime-back toolbox test surface`
  - `cc716c73` `feat: compose help and game link tool routes`
- likely first buttons to convert:
  - toolbox actions on the test page
  - game-link action cluster on the tools route
  - instance-side actions that still only emit activity text

Examples:

- open folder
- import/export
- file installation
- maintenance actions
- tool outputs

Done when:

- clicking a button performs a visible shell or file action instead of only adding a log line

Manual verification:

- click each migrated button
- confirm a file, folder, archive, config change, or external target actually appears

### Step 3. Finish launch-path cutover

Goal:

- make the replacement shell capable of real end-to-end launcher use for normal launching

Done when:

- the normal app path launches real instances using replacement-shell orchestration
- prompt flow, Java flow, and launch session flow behave as expected from the new shell

Manual verification:

- select a real instance
- satisfy prompts
- launch the game
- verify logs, process state, and post-launch shell behavior

### Step 4. Isolate cross-platform adapters

Goal:

- separate portable workflow logic from OS-specific shell behavior

Expected adapter areas:

- open file
- open folder
- save file
- shortcut creation
- protected data
- clipboard integration
- external URL/process opening

Done when:

- platform differences are isolated behind explicit services
- frontend view-model code no longer assumes Windows-only behavior

Manual verification:

- run the same shell actions on Windows, macOS, and Linux
- confirm equivalent user-visible behavior

### Step 5. Replace WPF-owned launcher behaviors

Goal:

- migrate remaining runtime-critical behaviors out of WPF page logic

Likely targets:

- launch-adjacent shell glue
- download/install execution glue
- instance maintenance actions
- update/packaging flows

Done when:

- WPF no longer owns the behavior needed for normal day-to-day launcher use

Manual verification:

- complete normal launcher workflows without falling back to WPF-specific pages or codepaths

### Step 6. Package and validate multi-platform launcher builds

Goal:

- ship the replacement launcher as a real application on all supported platforms

Done when:

- Windows, macOS, and Linux builds are produced
- basic launcher workflows pass on all three

Manual verification:

- install and launch the packaged app on each platform
- validate startup, navigation, config persistence, download flows, instance management, and a real game launch

## Immediate Next Engineering Slices

The next engineers should not take “finish the launcher” as one task. They should take one of these.

### Slice A. Replace remaining tool-page placeholders

Scope:

- `工具/测试`
- any tool controls that still only emit intent text

Reviewer can verify by:

- clicking each migrated tool button
- inspecting generated files, downloaded files, or visible shell outputs

### Slice B. Replace remaining instance-action placeholders

Scope:

- instance overview/setup/export actions that still stop at activity text

Reviewer can verify by:

- renaming, exporting, checking, or maintaining a real instance
- inspecting resulting files or config changes

### Slice C. Launch execution cutover spike-to-real pass

Scope:

- move from runtime-backed launch composition to runtime-backed launch execution

Reviewer can verify by:

- launching a real instance from the replacement shell
- checking game process, logs, and shell state

### Slice D. Cross-platform adapter matrix

Scope:

- document and implement platform-specific shell behavior behind service boundaries

Reviewer can verify by:

- running the same shell actions on at least two platforms and comparing outcomes

## Current Recommended Definition Of “Frontend Done”

The frontend migration should be considered complete only when all of the following are true:

- the Avalonia shell is the primary launcher UI
- major launcher workflows work from the replacement shell
- cross-platform shell behavior is explicit and tested
- WPF is no longer needed for normal operation
- the copied launcher design language is preserved
- runtime behavior is backed by portable services and adapters, not spike fixtures

## Suggested Commit Style

Keep frontend commits small and reviewable.

Good examples:

- `feat: wire tool test downloads to real shell actions`
- `feat: migrate instance overview actions to runtime adapters`
- `refactor: isolate cross-platform shell open/save adapters`
- `docs: refresh frontend cutover checklist`

Avoid:

- giant mixed commits that change multiple route families and platform concerns at once

## Latest Frontend Checkpoints

- `fb4c56c5` `refactor: isolate inspection-only spike workflows`
- `40467f7d` `refactor: add frontend inspection composition boundary`
- `c32d72b1` `docs: mark frontend phase 5 complete`
- `311b88b3` `feat: wire save detail and download surfaces`

## Bottom Line

The correct continuation point is no longer “prove another frontend slice can render.”

The correct continuation point is:

- make each copied route family truly usable
- replace remaining placeholder actions with real shell behavior
- cut over real launcher workflows
- isolate cross-platform adapters
- remove the remaining WPF dependency for normal use
