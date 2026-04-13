# Frontend Migration Plan

## Mission

The mission is now straightforward:

- migrate PCL-CE from the legacy WPF frontend to a fully working Avalonia frontend
- preserve the existing launcher design and controls
- move real behavior behind portable services and explicit shell adapters
- finish with a launcher that works on Windows, macOS, and Linux

This plan is intentionally broken into small, manually verifiable slices so engineers can deliver progress without creating unreviewable batches.

## Non-Negotiable Rules

### Copy, do not redesign

- keep the existing page structure
- keep the existing labels, hierarchy, and control language
- reuse the WPF launcher as the source of truth for visuals and interaction

### Backend owns policy

- launch policy, prompt policy, Java policy, crash policy, and install policy belong in backend services
- the frontend may compose, bind, route, and call shell actions

### Every slice must be verifiable

Each slice should end with:

- a passing build
- a manual test list
- visible real behavior in the running launcher

## Current Status As Of 2026-04-04

Completed:

- Phase 1: shell bootstrap runtime composition
- Phase 2: prompt command execution
- Phase 3: launch-state runtime composition
- Phase 4A: setup-page runtime composition and persistence
- Phase 4B: instance-page runtime composition and persistence
- Phase 4C: download-page and version-saves runtime composition
- Phase 5: frontend adapter cleanup and inspection separation

Recent checkpoints:

- `74be91e1` `feat: wire frontend install selection flow`
- `5e51a2be` `feat: add frontend install workflow primitives`
- `1aa6abfb` `feat: repair instance files from portable manifests`
- `b7e11a0a` `feat: cut over instance launch-side overview actions`
- `4a065f15` `refactor: isolate frontend platform adapters`
- `5ac85e61` `feat: cut over frontend launch execution`
- `7fad5b40` `feat: wire frontend track2 shell actions`
- `5b0ac628` `feat: report toolbox memory diagnostics`
- `ef3c0b92` `feat: wire instance overview runtime actions`
- `91228f81` `feat: add frontend shell dialog adapters`
- `311b88b3` `feat: wire save detail and download surfaces`
- `e96247f6` `feat: runtime-back toolbox test surface`
- `cc716c73` `feat: compose help and game link tool routes`
- `af689e45` `feat: wire instance resource and export actions`
- `f5d2a521` `feat: wire toolbox shell actions`

What that means today:

- the replacement shell is no longer mostly fixture-driven
- major route families already exist visually
- the tools route family now has dedicated runtime composition
- instance resource/server/export pages now perform several real file, clipboard, and archive actions from the replacement shell
- instance overview/actions now perform real rename, description, trash, patch, test-launch, launch-script export, and manifest-driven repair/reset flows from the replacement shell
- Track 2 shell-action parity is now effectively complete for the migrated tool/download/instance surfaces
- Track 1 route parity is effectively complete in the current frontend branch
- Track 4 adapter implementation is now in place for migrated shell actions, runtime path conventions, shortcut/script materialization, and protected key envelope decoding
- the remaining work is now mostly Track 5 WPF responsibility removal plus Track 6 packaging and multi-platform validation

## Repo Boundaries

### `PCL.Frontend.Spike`

Owns:

- Avalonia shell
- copied UI
- view-model composition
- frontend-side shell actions
- inspection tooling

Should not own:

- launcher policy
- permanent replay/sample coupling

### `PCL.Core` and `PCL.Core.Backend`

Own:

- portable launcher behavior
- workflow planning
- runtime state services
- cross-platform abstractions where possible

### `Plain Craft Launcher 2`

Still owns:

- source-of-truth visuals
- remaining legacy behavior not yet moved

Should gradually lose ownership of:

- day-to-day launcher workflows

## Migration Tracks

The work is now organized into six small tracks.

## Track 1. Route Parity

### Goal

- every major route family should have route-local runtime composition instead of page-local placeholder state

### Scope

- tools pages
- remaining instance detail/action pages
- any route still depending on fake sample data for its primary content

### Deliverables

- route-local composition service or composition method
- view-model bindings populated from runtime files/services
- no UI redesign

### Manual verification

1. Open the app.
2. Navigate to the migrated route.
3. Confirm the page shows real launcher data.
4. Change the underlying file/config/runtime input.
5. Refresh or reopen the route and confirm the page updates.

### Suggested slice size

- one route family per commit

### Status on 2026-04-04

Completed in this track:

- tools pages now use `FrontendToolsCompositionService`
- toolbox/test defaults now come from launcher config instead of page-local demo state
- help content now comes from bundled help data instead of hardcoded sample topics
- game-link route now starts from config- and instance-backed baseline state instead of fake member/sample state
- instance overview actions no longer stop at pure activity-feed intent text
- download resource/detail routes no longer rely on sample entries for their main list content

Track 1 status:

- complete in the current frontend branch
- next work should stay in Track 2 and Track 3 unless a newly discovered route falls back to placeholder-only primary data

Recommended owner split:

- continue with migrated tools and install-entry actions because the route composition is already in place
- prioritize game-link and remaining install-entry shell actions before widening scope to broader cutover work

## Track 2. Shell Action Parity

### Goal

- replace remaining placeholder button behavior with real shell actions

### Scope

- open/import/export actions
- maintenance actions
- tool actions
- instance actions

### Deliverables

- real file picker, folder picker, open-target, export, import, or maintenance behavior
- failures surfaced to the user via the shell instead of silent no-op behavior

### Manual verification

1. Click the migrated button.
2. Confirm a real file, folder, config change, or external action occurs.
3. Confirm failure cases show clear feedback.

### Suggested slice size

- one button group or one page action cluster per commit

### Status on 2026-04-04

Completed in this track:

- toolbox test page now has real shell/file behavior for cleanup, daily-luck output, launch-count output, and shortcut creation
- memory optimization now exports explicit diagnostics instead of only recording intent text
- instance overview actions now perform real rename, description, trash, patch, and report-generation flows
- game-link actions now use prompt, clipboard, config, FAQ, and exported session/diagnostic behavior from the replacement shell
- download modpack install now copies a local pack into the launcher `versions` folder
- favorites management, toolbox head export, and instance-profile-template actions now create reviewable files instead of pure intent logs

Still required before this track can be called fully closed:

- launch-adjacent overview buttons still export runtime context artifacts instead of full end-to-end execution, which should converge with Track 3

Track 2 status:

- effectively complete in the current frontend branch for non-launch migrated surfaces
- new work should move to Track 3 unless a newly discovered copied button still falls back to intent-only behavior

## Track 3. Launch Cutover

### Goal

- move from runtime-backed launch composition to real replacement-shell launch execution

### Scope

- launch session startup
- log/session visibility
- Java acquisition flow
- prompt-driven launch continuation
- post-launch shell behavior

### Deliverables

- real game launch from the replacement shell on the normal app path
- prompt and launch state remain backend-driven

### Manual verification

1. Select a real instance.
2. Launch it from the Avalonia shell.
3. Confirm required prompts appear and work.
4. Confirm the game process starts.
5. Confirm launcher logs/session state update correctly.

### Suggested slice size

- one launch subsystem per commit

### Status on 2026-04-04

Completed in this track:

- launch composition now builds real argument/session/post-launch plans for the selected instance instead of placeholder launch artifacts
- custom command execution, game process startup, launcher visibility behavior, session summaries, and realtime log capture now run from the replacement shell
- launch prompt continuation now preserves already-dismissed prompts during the current attempt instead of recreating them
- Java runtime discovery now rejects the macOS `/usr/bin/java` stub and finds real host runtimes such as Homebrew OpenJDK
- missing-Java prompt flow can materialize a Mojang runtime and now performs that download work without freezing the frontend UI

Manual verification completed on this track:

1. Opened the Avalonia app against the real launcher folder `/Users/theunknownthing/Library/Application Support/SJMCL/minecraft`.
2. Selected real instance `1.21.10`.
3. Confirmed launch and startup prompts render from the copied shell flow and can be advanced.
4. Confirmed the replacement shell generated a real launch command using `/opt/homebrew/opt/openjdk/bin/java`.
5. Confirmed the game window opened as `Minecraft 1.21.10`.
6. Confirmed the replacement shell wrote session output under `~/.config/PCL/Log/session-20260404-233003.log`.

Track 3 status:

- complete on the current frontend branch for the normal launch path
- next work should move to Track 4 and Track 5 unless a newly discovered launch edge case still falls back to WPF-owned behavior

## Track 4. Cross-Platform Adapter Isolation

### Goal

- make all OS-facing behavior explicit and portable

### Scope

- open file
- save file
- open folder
- external URL/process open
- clipboard behavior
- protected storage
- shortcut creation
- path and packaging differences

### Deliverables

- platform-sensitive behavior isolated behind services
- frontend code no longer assumes Windows-specific APIs or paths

### Manual verification

1. Run the same shell action on Windows and one non-Windows platform.
2. Confirm equivalent behavior.
3. Confirm unsupported behavior fails explicitly rather than implicitly.

### Suggested slice size

- one adapter family per commit

### Status on 2026-04-04

Completed in this track:

- open-file and open-folder picker behavior already routes through the explicit Avalonia shell action service
- clipboard reads and writes already route through the explicit Avalonia shell action service
- launcher app-data path selection, external target opening, shortcut creation, Unix executable marking, command script extension selection, and default Java path hints now route through `FrontendPlatformAdapter`
- stored launcher key envelope decoding is now shared through `LauncherStoredKeyEnvelopeService` instead of being duplicated in frontend-local Windows DPAPI branches
- migrated frontend view-model code no longer carries inline OS branches for shortcut creation, command script export, or default Java path selection

Manual verification completed on this track:

1. Built `PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` successfully on macOS.
2. Ran an ad hoc verification harness against the built frontend assembly.
3. Confirmed the adapter resolves launcher app data to `~/.config/PCL` on macOS.
4. Confirmed shortcut creation emits a real `.command` file with executable Unix mode bits.
5. Confirmed exported command scripts receive executable Unix mode bits.
6. Confirmed explicit external-target open succeeds on macOS for a real local folder.

Track 4 status:

- implementation is effectively complete in the current frontend branch
- Windows and Linux runtime validation is still required before the cross-platform matrix can be called fully closed
- next code work should move to Track 5 unless a newly discovered copied surface still embeds OS-specific UI logic

## Track 5. WPF Responsibility Removal

### Goal

- remove remaining WPF ownership of normal launcher workflows

### Scope

- behaviors still trapped in WPF page code
- launch-adjacent shell glue
- download/install glue
- maintenance and recovery actions

### Deliverables

- frontend and backend own the behavior needed for daily launcher use
- WPF is no longer needed for regular workflows

### Manual verification

1. Complete the target workflow entirely from the replacement shell.
2. Confirm no hidden WPF-only path is required.

### Suggested slice size

- one workflow family per commit

Status on 2026-04-04:

- instance overview `测试游戏` now starts the real replacement-shell launch path from the copied overview surface instead of exporting a placeholder report
- instance overview `导出启动脚本` now writes the actual generated launch script from the portable session plan
- instance overview `补全文件` and `重置实例` now use a portable manifest-driven repair flow that refreshed the real `1.21.10` instance under `/Users/theunknownthing/Library/Application Support/SJMCL/minecraft`
- ad hoc verification against that real instance reused 4,477 files and re-downloaded the asset index without invoking WPF code
- the copied download and instance install surfaces now own managed selection/apply behavior for Minecraft, Fabric, Legacy Fabric, Quilt, LabyMod, Fabric API, Legacy Fabric API, and QFAPI / QSL through `FrontendInstallWorkflowService`
- ad hoc verification on 2026-04-05 created a temporary `/Users/theunknownthing/Library/Application Support/SJMCL/minecraft/versions/codex-track5-verify`, applied Fabric plus Fabric API on top of Minecraft `1.21.10`, then reapplied the same instance as Quilt plus QFAPI / QSL and observed the managed addon jar switch from `fabric-api-0.138.4+1.21.10.jar` to `quilted-fabric-api-11.0.0-alpha.3+0.102.0-1.21.jar`
- that managed install verification reused 4,397 files on the first apply and 4,478 files on the second apply while writing the migrated manifest and instance config without invoking WPF code
- the temporary verification instance was removed after inspection so the real launcher folder returned to its prior state
- the copied download and instance install surfaces now also own the unmanaged installer family for Forge, NeoForge, Cleanroom, LiteLoader, OptiFine, and OptiFabric, while preserving the copied compatibility rules from `PageDownloadInstall.xaml.vb`
- ad hoc verification on 2026-04-05 created temporary instances under `/Users/theunknownthing/Library/Application Support/SJMCL/minecraft/versions` for `Forge 1.21.1`, `NeoForge 1.21.1`, `Cleanroom 1.12.2`, `LiteLoader 1.12.2`, and `Fabric 1.20.1 + OptiFine + OptiFabric`, and each apply wrote the migrated manifest without invoking WPF code
- that unmanaged-install verification observed the expected loader libraries for Forge, NeoForge, Cleanroom, and LiteLoader, and wrote `mods/optifabric-1.14.3.jar` plus `mods/OptiFine_1.20.1_HD_U_I5.jar` for the Fabric + OptiFine case
- the migrated repair flow now reuses valid installer-local libraries during forced refreshes instead of trying to redownload mismatched remote artifacts, which removes the Cleanroom failure hit during the first real-instance pass
- the temporary verification instances were removed after inspection so the real launcher folder returned to its prior state
- a forced Cleanroom `RunRepair + ForceCoreRefresh` pass moved past the earlier local-library error and into the wider core-refresh workload; that long-running asset-heavy pass was not waited through to final completion during this checkpoint
- Track 5 no longer has a known WPF-owned install-family gap, so next work should move to Track 6 packaging and broader platform validation unless a new fallback path is discovered

## Track 6. Multi-Platform Packaging And Validation

### Goal

- produce a real multi-platform launcher instead of only a development shell

### Scope

- Windows packaging
- macOS packaging
- Linux packaging
- runtime validation on all supported platforms

### Deliverables

- packaged builds
- platform-specific validation checklist
- known-gaps list kept small and explicit

### Manual verification

1. Install the packaged launcher.
2. Start it on the target OS.
3. Validate startup, navigation, persistence, instance management, download actions, and one real launch.

### Suggested slice size

- one platform or one packaging subsystem per commit

## Recommended Order

Engineers should usually work in this order:

1. Finish remaining route parity.
2. Finish remaining shell action parity.
3. Cut over real launch execution.
4. Isolate and test cross-platform adapters.
5. Remove remaining WPF workflow ownership.
6. Package and validate all supported platforms.

## Immediate Next Slices

These are the best next tasks after the current state of the repo.

### Slice 1. Real launch execution cutover

Why first:

- highest value milestone
- main blocker between “replacement shell” and “real launcher”

Expected reviewer test:

- launch a real instance from the Avalonia shell and verify end-to-end behavior

### Slice 2. Cross-platform shell adapter pass

Why second:

- necessary before claiming multi-platform launcher support

Expected reviewer test:

- run the same migrated shell actions on multiple OSes and compare outcomes

Status on 2026-04-04:

- implementation landed in `4a065f15`; the remaining work here is host-matrix validation rather than another frontend adapter refactor

### Slice 3. Remove remaining WPF workflow ownership

Why third:

- needed to finish normal day-to-day launcher workflows without hidden legacy dependencies

Expected reviewer test:

- complete the target workflow entirely from the replacement shell and confirm no WPF-only path is required

### Slice 4. Packaging and multi-platform validation

Why fourth:

- this is the final confidence gate before claiming a real replacement launcher

Expected reviewer test:

- install the packaged app, validate startup/navigation/persistence, and complete one real launch on the target OS

## Definition Of Done For Each Slice

A slice is only complete if all of these are true:

- the project builds
- the copied UI remains intact
- real behavior replaces placeholder behavior
- a reviewer can test it manually in the launcher
- the code respects backend/frontend boundaries
- the change is documented if it changes the next recommended path

## Definition Of Done For The Full Frontend Migration

The frontend migration is complete when:

- the Avalonia shell is the primary launcher UI
- normal users can use it for daily launcher workflows
- launch, download, setup, tools, and instance flows are real
- cross-platform shell behavior is isolated and validated
- WPF is no longer needed for standard operation
- the original launcher’s visual language is still recognizable and preserved

## Current Manual Validation Checklist

This is the minimum review checklist engineers should keep green while migrating.

1. `dotnet build PCL.Frontend.Spike/PCL.Frontend.Spike.csproj`
2. Open the desktop shell entry and navigate all major route groups.
3. Validate at least one migrated action from the changed route family.
4. Confirm real files/config/runtime state are being used.
5. Confirm no obvious UI redesign slipped in.

Verification note:

- in the current workspace, `dotnet run PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` may enter the inspection/replay host instead of the interactive desktop shell, so route-click verification should be done from the desktop app entry rather than assuming the CLI host is the real UI path

## Commit Guidance

Prefer frequent, narrow commits.

Good examples:

- `feat: wire tool test downloads to real shell actions`
- `feat: migrate instance maintenance actions`
- `feat: wire instance overview shell actions`
- `refactor: isolate launcher shell adapters`
- `docs: refresh frontend cutover checklist`

Avoid:

- broad mixed commits across unrelated route families
- “cleanup” commits that hide behavior changes

## Bottom Line

The work should now feel like product migration, not spike extension.

The next engineers should keep asking:

- what real launcher behavior can we make testable in the replacement shell this week?
- how can a reviewer validate it without reading all the code?
- how do we preserve the original launcher UI while moving one more workflow out of WPF?
