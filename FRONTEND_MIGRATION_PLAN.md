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

Recent checkpoint:

- `311b88b3` `feat: wire save detail and download surfaces`

What that means today:

- the replacement shell is no longer mostly fixture-driven
- major route families already exist visually
- the remaining work is mostly real-behavior parity and cross-platform cutover

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

### Slice 1. Finish remaining tool-page actions

Why first:

- small scope
- highly visible
- easy for reviewers to test manually

Expected reviewer test:

- click each migrated tool action and inspect the resulting file/output/folder behavior

### Slice 2. Finish remaining instance-action placeholders

Why second:

- keeps instance management usable from the replacement shell

Expected reviewer test:

- perform the migrated instance action and verify the resulting launcher or filesystem state

### Slice 3. Real launch execution cutover

Why third:

- highest value milestone
- main blocker between “replacement shell” and “real launcher”

Expected reviewer test:

- launch a real instance from the Avalonia shell and verify end-to-end behavior

### Slice 4. Cross-platform shell adapter pass

Why fourth:

- necessary before claiming multi-platform launcher support

Expected reviewer test:

- run the same migrated shell actions on multiple OSes and compare outcomes

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
2. Open the shell and navigate all major route groups.
3. Validate at least one migrated action from the changed route family.
4. Confirm real files/config/runtime state are being used.
5. Confirm no obvious UI redesign slipped in.

## Commit Guidance

Prefer frequent, narrow commits.

Good examples:

- `feat: wire tool test downloads to real shell actions`
- `feat: migrate instance maintenance actions`
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
