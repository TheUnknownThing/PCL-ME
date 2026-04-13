# PCL-CE Portability Handoff

## Executive Summary

Status as of 2026-04-04:

- the portable backend is real
- the Avalonia replacement shell is real
- copied launcher UI exists for most major routes in `PCL.Frontend.Spike`
- startup, prompt, launch, setup, instance, download, and version-saves composition are now partially or mostly runtime-backed
- the tools route family now has a dedicated runtime composition path
- instance resource/server/export surfaces now perform several real file, clipboard, and archive actions from the replacement shell
- instance overview actions now perform real rename, description, trash, patch, and artifact-export flows from the replacement shell
- instance overview launch-adjacent actions now start a real replacement-shell test launch and export the actual generated launch script instead of launch-context placeholders
- instance overview recovery actions now repair and reset the selected instance by reusing portable manifest downloads for core files, libraries, natives, asset indexes, and missing assets
- download resource/detail routes no longer fall back to sample primary content for their main lists
- toolbox test page continues moving from intent-only buttons to real shell/file outputs, and memory optimization now exports explicit diagnostics instead of a pure intent log
- tools/game-link actions now use real prompt, clipboard, config, FAQ, and exported session/diagnostic behavior instead of synthetic activity-only output
- download modpack install now copies a local pack into the launcher `versions` folder from the replacement shell
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

The visible Track 2 buttons on migrated tool/download/instance surfaces are now mostly backed by real shell actions.

The main remaining gap in this area is now:

- launch execution is still exported as context/report artifacts rather than full replacement-shell process orchestration

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

- tools route family: done for Track 1
- instance resource/server/export/overview surfaces are now runtime-backed and no longer Track 1 blockers
- download resource/detail routes no longer keep sample-driven primary content for their main lists
- Track 1 route parity is effectively complete in the current frontend branch; the next owner should stay focused on Track 2 and Track 3 work

Manual verification:

- use real launcher files/config and confirm each route shows live data
- change the source files, refresh the route, and confirm the view updates

### Step 2. Finish shell action parity

Goal:

- replace remaining activity-feed-only actions with real shell behavior where practical

Track 2 status on 2026-04-04:

- the current frontend branch now has a dedicated shell-action pass for the previously missing game-link, toolbox/head export, favorites management, instance-profile-template, and download modpack install buttons
- recent frontend checkpoints for this handoff:
  - `7fad5b40` `feat: wire frontend track2 shell actions`
  - `5b0ac628` `feat: report toolbox memory diagnostics`
  - `ef3c0b92` `feat: wire instance overview runtime actions`
  - `f5d2a521` `feat: wire toolbox shell actions`
- the next owner should only return to Track 2 if a newly discovered copied button still falls back to pure intent logging
- otherwise the recommended next step is Step 3: launch-path cutover

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

Status on 2026-04-04:

- complete in `codex/frontend-track3-launch-cutover`
- the normal Avalonia app path now launches the real macOS instance at `/Users/theunknownthing/Library/Application Support/SJMCL/minecraft/versions/1.21.10`
- launch prompts now continue correctly without recreating the same prompt in the same attempt
- Java discovery now prefers real installed runtimes such as `/opt/homebrew/opt/openjdk/bin/java` instead of the macOS `/usr/bin/java` stub
- missing-Java prompt flow still works when no executable runtime is selected, and prompt-triggered download now runs without freezing the UI thread
- replacement-shell launch artifacts now record the real launch command and session summary under `~/.config/PCL`

Manual verification:

- select a real instance
- satisfy prompts
- launch the game
- verify logs, process state, and post-launch shell behavior

Verified on 2026-04-04:

- launched `Minecraft 1.21.10` from the Avalonia shell
- confirmed `~/.config/PCL/LatestLaunch.bat` used `/opt/homebrew/opt/openjdk/bin/java`
- confirmed prompt flow advanced through startup and launch prompts and opened the real game window
- confirmed session log creation at `~/.config/PCL/Log/session-20260404-233003.log`

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

Status on 2026-04-04:

- complete in `codex/frontend-track4-adapters` at the implementation level
- launcher app-data path selection, external target opening, shortcut creation, command script extension selection, Unix executable marking, and default Java path hints now route through `PCL.Frontend.Spike/Workflows/FrontendPlatformAdapter.cs`
- open-file/open-folder picker and clipboard behavior continue to route through `PCL.Frontend.Spike/Workflows/FrontendShellActionService.cs`
- stored launcher key envelope decoding now routes through `PCL.Core/App/Essentials/LauncherStoredKeyEnvelopeService.cs` instead of frontend-local Windows-only decode branches
- migrated frontend view-model code no longer carries inline platform branches for shortcut creation, exported launch script naming, or default Java path selection

Manual verification:

- run the same shell actions on Windows, macOS, and Linux
- confirm equivalent user-visible behavior

Verified on 2026-04-04:

- `dotnet build PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` passed on macOS
- an ad hoc verification harness against the built frontend assembly resolved launcher app data to `~/.config/PCL`
- the same harness created a real `.command` shortcut file and confirmed executable Unix mode bits
- the same harness marked an exported script file executable on macOS
- the same harness successfully opened a real local folder through the explicit external-target adapter path
- Windows and Linux runtime verification are still needed before the full adapter matrix can be called closed

### Step 5. Replace WPF-owned launcher behaviors

Goal:

- migrate remaining runtime-critical behaviors out of WPF page logic

Likely targets:

- launch-adjacent shell glue
- download/install execution glue
- instance maintenance actions
- update/packaging flows

Status on 2026-04-04:

- instance overview `测试游戏` now routes into the real replacement-shell launch flow instead of writing a test-context artifact
- instance overview `导出启动脚本` now writes the real generated launch script content from the portable session plan
- instance overview `补全文件` and `重置实例` now execute a portable manifest-driven repair path that refreshes the client jar, libraries, natives, asset index, and missing assets without falling back to WPF
- recent Track 5 checkpoints for the managed install workflow slice:
  - `5e51a2be` `feat: add frontend install workflow primitives`
  - `74be91e1` `feat: wire frontend install selection flow`
  - `2a9b74a0` `feat: add frontend unmanaged install workflow`
  - `99c6f076` `fix: finish track 5 installer migration`

Verified on 2026-04-05:

- `dotnet build PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` passed on macOS after wiring the copied install cards to the new workflow
- the frontend now owns the copied download/instance install selection flow for Minecraft, Fabric, Legacy Fabric, Quilt, LabyMod, Fabric API, Legacy Fabric API, and QFAPI / QSL through `FrontendInstallWorkflowService` and the shell dialog adapters
- a real verification pass against `/Users/theunknownthing/Library/Application Support/SJMCL/minecraft` created a temporary `versions/codex-track5-verify/codex-track5-verify.json`, first as Fabric with `mods/fabric-api-0.138.4+1.21.10.jar`, then reapplied the same instance as Quilt with `mods/quilted-fabric-api-11.0.0-alpha.3+0.102.0-1.21.jar`
- the managed install workflow reused 4,397 existing files on the first apply and 4,478 existing files on the second apply while switching the loader manifest and managed addon jar without falling back to WPF code
- the temporary verification instance was removed after inspection so the real launcher folder was not left with extra test clutter
- the frontend now also owns the copied unmanaged installer family for Forge, NeoForge, Cleanroom, LiteLoader, OptiFine, and OptiFabric, including the legacy compatibility rules copied from `PageDownloadInstall.xaml.vb`
- a second real verification pass against the same launcher directory created temporary instances for `Forge 1.21.1`, `NeoForge 1.21.1`, `Cleanroom 1.12.2`, `LiteLoader 1.12.2`, and `Fabric 1.20.1 + OptiFine + OptiFabric`; those instances wrote the migrated manifests, produced the expected loader libraries, and in the Fabric case wrote `mods/optifabric-1.14.3.jar` plus `mods/OptiFine_1.20.1_HD_U_I5.jar`
- the Forge-family manifest cleanup now strips missing local-only artifacts, OptiFabric choices now resolve from the live OptiFabric file feed instead of the dead Modrinth slug, and instance repair now reuses valid installer-local libraries instead of force-redownloading the wrong remote artifact
- the temporary unmanaged-installer verification instances were removed after inspection so the real launcher folder was not left with extra test clutter
- a forced Cleanroom `RunRepair + ForceCoreRefresh` pass progressed past the earlier local-library failure and into the broader core refresh workload; that long-running asset-heavy pass was not waited through to final completion during this checkpoint
- the copied install family no longer has a known WPF-owned Track 5 gap; next work should move to Track 6 packaging and broader platform validation unless a new fallback surface is discovered

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

### Slice A. Launch execution cutover spike-to-real pass

Scope:

- move from runtime-backed launch composition to runtime-backed launch execution

Reviewer can verify by:

- launching a real instance from the replacement shell
- checking game process, logs, and shell state

### Slice B. Cross-platform adapter matrix

Scope:

- document and implement platform-specific shell behavior behind service boundaries

Reviewer can verify by:

- running the same shell actions on at least two platforms and comparing outcomes

Status on 2026-04-04:

- implementation landed in `4a065f15` `refactor: isolate frontend platform adapters`
- the next owner should treat remaining work here as validation on additional hosts, not another large frontend adapter rewrite

### Slice C. Remove remaining WPF workflow ownership

Scope:

- launch-adjacent shell glue
- download/install execution glue that still depends on legacy codepaths
- remaining WPF-owned day-to-day workflows

Reviewer can verify by:

- completing the target workflow entirely from the replacement shell without falling back to WPF-only behavior

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

- `1aa6abfb` `feat: repair instance files from portable manifests`
- `b7e11a0a` `feat: cut over instance launch-side overview actions`
- `4a065f15` `refactor: isolate frontend platform adapters`
- `7fad5b40` `feat: wire frontend track2 shell actions`
- `5b0ac628` `feat: report toolbox memory diagnostics`
- `ef3c0b92` `feat: wire instance overview runtime actions`
- `91228f81` `feat: add frontend shell dialog adapters`
- `fb4c56c5` `refactor: isolate inspection-only spike workflows`
- `40467f7d` `refactor: add frontend inspection composition boundary`
- `c32d72b1` `docs: mark frontend phase 5 complete`
- `311b88b3` `feat: wire save detail and download surfaces`
- `af689e45` `feat: wire instance resource and export actions`
- `f5d2a521` `feat: wire toolbox shell actions`

## Bottom Line

The correct continuation point is no longer “prove another frontend slice can render.”

The correct continuation point is:

- keep each copied route family truly usable
- cut over real launcher workflows
- isolate cross-platform adapters
- remove the remaining WPF dependency for normal use
