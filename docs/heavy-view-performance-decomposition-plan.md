# Heavy View Performance Decomposition Plan

## Goal

Reduce UI-thread pressure and route-transition cost by decomposing the remaining large Avalonia views, especially the legacy compatibility path centered on `PclShellContentPanel.axaml`, while preserving the current `PCL.Neo`-aligned shell style and animation behavior.

This plan is the companion to:

- `/Users/theunknownthing/PCL-CE/docs/standard-shell-host-refactor-plan.md`

The host-shell refactor established swappable standard-shell hosts. This document covers the next layer: breaking up the still-heavy views and view-model surfaces that keep the visual tree and binding graph larger than `PCL.Neo`.

## Why This Is Still Needed

Even after the host swap refactor, CE still carries more rendering weight than `PCL.Neo` because several routes still resolve through a legacy compatibility right pane:

- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/LegacyStandardShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`

That compatibility path still hosts a very large right-side view and keeps route-family logic concentrated in one place.

The difference in scale is clear:

- `PCL.Neo` entire `Views` folder: about 431 lines of XAML total
- CE `PclShellContentPanel.axaml`: about 3,315 lines by itself
- CE `MainWindow.axaml`: about 459 lines
- CE `InstanceSetupShellRightPaneView.axaml`: about 419 lines

The problem is no longer just shell animation code. The remaining issue is view weight:

- large live XAML trees
- large numbers of bindings on every route
- large route-family views with too many conditional sections
- view-model partials that continue to publish broad UI state even when a route only needs a subset

## Reference Files

Primary `PCL.Neo` references:

- `/Users/theunknownthing/PCL.Neo/PCL.Neo/Views/MainWindow.axaml`
- `/Users/theunknownthing/PCL.Neo/PCL.Neo/Views/MainWindow.axaml.cs`

Primary CE implementation files:

- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/MainWindow.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/MainWindow.axaml.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/LegacyStandardShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.ShellPanes.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Navigation.cs`

Large CE view files to target next:

- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/InstanceSetupShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/InstanceOverviewShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/InstanceResourceShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/InstanceExportShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/InstanceInstallShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/VersionSaveDatapackShellRightPaneView.axaml`

Large CE view-model partials that still influence UI weight:

- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceActions.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.DownloadResourceCatalog.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Prompts.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.InstanceOverview.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.InstanceContent.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SetupComposition.cs`

## Performance Targets

The team should optimize for these outcomes, in this order:

1. Remove `PclShellContentPanel.axaml` from the active standard-shell path completely.
2. Ensure a route swap only instantiates the pane view for the active route family.
3. Reduce binding fan-out by shrinking route-specific views and trimming compatibility surface properties.
4. Keep host-level animation unchanged in style and behavior.
5. Match `PCL.Neo` structurally where possible, without rewriting CE features around aesthetics.

## Current Problem Areas

### A. Legacy compatibility right pane

`LegacyStandardShellRightPaneView.axaml` still hosts `PclShellContentPanel`, so every unmigrated setup/download/tools route stays on the old heavy path.

Impact:

- large visual tree
- higher binding churn on navigation
- slower transitions than `PCL.Neo`
- harder reasoning about which route actually owns which UI

### B. Large route-family pane views

Some dedicated panes are still large enough to become the next bottleneck after the legacy panel is removed.

Current examples:

- `InstanceSetupShellRightPaneView.axaml`
- `InstanceOverviewShellRightPaneView.axaml`
- `InstanceResourceShellRightPaneView.axaml`
- `InstanceExportShellRightPaneView.axaml`
- `InstanceInstallShellRightPaneView.axaml`
- `VersionSaveDatapackShellRightPaneView.axaml`

These are better than one mega-view, but some should still be decomposed into reusable subviews or smaller section controls.

### C. High-fan-out view-model partials

Several `FrontendShellViewModel` partials still own broad UI state, route-specific prompts, and composition helpers that are larger than the view currently displayed.

This matters because:

- even with smaller XAML, bindings still point into one large shell VM
- route changes can still raise too many properties
- some panes depend on shared state bags instead of narrow pane-specific models

## Architecture Direction

### View structure

The end state should follow this pattern:

- `MainWindow` owns fixed hosts
- each active route resolves to one left pane VM and one right pane VM
- each right-pane VM resolves to one focused view
- large route-family views are split into section controls only when needed
- no route should require a hidden mega-view to stay alive in the background

### View-model structure

The end state should also reduce shell-level state pressure:

- keep `FrontendShellViewModel` as the shell coordinator
- move route-family display state into focused pane VMs or section VMs
- keep commands and core collections reusable
- stop publishing broad compatibility booleans once all affected panes are migrated

## Decomposition Rules

These rules apply to every engineer working from this plan:

- Do not redesign visuals. Keep the existing `PCL.Neo`-aligned look.
- Do not move animations into child sections. Host-level transitions stay in `MainWindow`.
- Prefer extracting existing XAML blocks over rewriting layouts from scratch.
- Split by route family first, then by section only where the pane is still too large.
- Reuse existing commands and data sources before creating new ones.
- Avoid creating one tiny VM per trivial label block. Decompose where it meaningfully reduces live tree size or binding scope.

## Parallel Workstreams

The work below is designed so multiple engineers can move concurrently with low merge conflict risk.

### Workstream A: Eliminate Legacy Compatibility Right Pane

Owner:

- Engineer A

Status:

- Completed on April 6, 2026.
- Standard-shell generic and fallback routes now resolve through `GenericStandardShellRightPaneViewModel`.
- `LegacyStandardShellRightPaneView` and `PclShellContentPanel` are no longer part of the active standard-shell path.

Goal:

- Remove `PclShellContentPanel` from the active standard-shell path completely.

Scope:

- Replace remaining legacy fallback usage in `ResolveStandardRightPane`.
- Create dedicated right-pane views and VMs for all still-unmigrated setup/download/tools routes.
- Delete the compatibility host only after all remaining routes are covered.

Primary files:

- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.ShellPanes.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/LegacyStandardShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`

Deliverables:

- no active route resolves to `LegacyStandardShellRightPaneViewModel`
- `PclShellContentPanel` is no longer required for standard-shell rendering
- compatibility view can be deleted or reduced to dead-code cleanup only

Dependencies:

- none beyond the already-landed host-shell contract

Write boundary:

- Engineer A owns `FrontendShellViewModel.ShellPanes.cs`
- no other stream should edit fallback-resolution logic without coordinating through this owner

### Workstream B: Setup Family Pane Extraction

Owner:

- Engineer B

Status:

- Completed on April 6, 2026.
- Setup launch, about, feedback, log, update, game link, game manage, launcher misc, java, and UI routes now render through dedicated right-pane views.
- `PclShellContentPanel.axaml` no longer carries setup-family sections, so the remaining compatibility panel only keeps non-setup content for other workstreams.

Goal:

- Replace setup-family content inside `PclShellContentPanel` with focused right-pane views.

Scope:

- Extract setup subpages:
  - launch
  - about
  - feedback
  - log
  - update
  - game link
  - game manage
  - launcher misc
  - java
  - UI
- Group shared layout where possible, but not at the expense of bringing back a mega-view.

Primary files:

- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SetupComposition.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SetupSettings.cs`
- new files under `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/`

Deliverables:

- setup routes render through dedicated pane views
- no setup route depends on `PclShellContentPanel`
- style matches current CE shell and `PCL.Neo`-aligned look

Dependencies:

- coordinate pane registration with Workstream F

Write boundary:

- Engineer B owns setup-family right-pane views and related setup-family pane VM additions

### Workstream C: Download and Tools Pane Extraction

Owner:

- Engineer C

Goal:

- Replace remaining download/tools content inside the legacy panel with dedicated route-family panes.

Scope:

- Extract:
  - download install
  - download catalog
  - download resource
  - download favorites
  - tools family surfaces still using the legacy panel
- Pay special attention to long lists and card grids that may need virtualization or deferred section loading later.

Primary files:

- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.DownloadResourceCatalog.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.ToolSurfaces.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.ToolsComposition.cs`
- new files under `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/`

Deliverables:

- download/tools routes no longer rely on the legacy panel
- heavy lists are isolated in dedicated views
- shared templates can be reused without reintroducing a giant all-routes-live tree

Dependencies:

- coordinate pane registration with Workstream F

Write boundary:

- Engineer C owns download/tools right-pane views and related pane VM additions

Status update:

- Completed on 2026-04-06.
- Added dedicated right-pane views for download install, download catalog, download resource, download favorites, tools game link, and tools test.
- Removed the download/tools surface blocks from `PclShellContentPanel.axaml`, so the compatibility panel no longer carries Engineer C routes.
- `dotnet build /Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` passed after the extraction.

### Workstream D: Second-Pass Decomposition of Large Dedicated Panes

Owner:

- Engineer D

Status:

- Completed on April 6, 2026.
- Reduced the route-host files for instance setup, overview, resource, export, install, and version-save datapack panes to thin shell hosts backed by focused section controls under `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/Sections/`.
- Extracted a shared `ResourceEntryCardView` for the duplicated resource/datapack entry rows, while keeping route-specific headers, empty states, and option groups in focused section controls.
- `dotnet build /Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` passed after the decomposition.

Goal:

- Split the largest already-migrated dedicated panes into smaller section controls where this reduces live tree size or binding scope.

Priority files:

- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/InstanceSetupShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/InstanceOverviewShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/InstanceResourceShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/InstanceExportShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/InstanceInstallShellRightPaneView.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/VersionSaveDatapackShellRightPaneView.axaml`

Suggested decomposition pattern:

- keep one route host per route family
- move repeated or bulky sections into focused reusable controls
- avoid section extraction if it only creates noise and no meaningful reduction

Deliverables:

- largest right-pane views reduced in size
- repeated section UI extracted into reusable controls where justified
- no visual regressions

Dependencies:

- can proceed immediately because these panes are already active-path views

Write boundary:

- Engineer D owns the files listed above and any new section controls they introduce

### Workstream E: View-Model Weight Reduction

Owner:

- Engineer E

Status:

- Completed on April 6, 2026.
- Route changes now resolve the standard-shell pane descriptors before route-family refresh logic runs, and only the active right-pane family is refreshed during navigation.
- Removed the shell-level setup/download/tools compatibility booleans in favor of descriptor-kind checks, and narrowed collection-state notifications to the active right pane instead of broadcasting every pane-family flag on each route change.
- `dotnet build /Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` passed after the cleanup.

Goal:

- Reduce broad shell-level UI state and property churn that still feeds heavy panes.

Scope:

- audit which properties are only needed by one pane family
- move route-specific display state from the shell VM into focused pane VMs or helper models
- reduce compatibility shims once all remaining routes are migrated
- trim unnecessary `RaisePropertyChanged` fan-out on route changes

Primary files:

- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Navigation.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceActions.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.DownloadResourceCatalog.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Prompts.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.InstanceOverview.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.InstanceContent.cs`

Deliverables:

- narrower pane-facing state contracts
- reduced route-change property fan-out
- fewer compatibility flags once extraction is complete

Dependencies:

- best started after Workstreams B and C define the final pane coverage

Write boundary:

- Engineer E owns VM-state cleanup and should not redesign view structure

### Workstream F: Shared Section Controls and Template Hygiene

Owner:

- Engineer F

Status:

- Completed on April 6, 2026.
- Centralized standard-shell right-pane construction in `ShellPaneTemplateRegistry`, so new pane types now land through one shared registration/factory path instead of duplicating template and VM-resolution edits.
- Added a reusable `PclActionSummaryRow` control for the repeated catalog/favorites action rows to keep shared right-pane list sections visually aligned without growing route hosts again.
- `dotnet build /Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` passed after the cleanup.

Goal:

- Prevent repeated layout chunks from drifting while keeping views small and composable.

Scope:

- create reusable section controls only for patterns repeated across multiple panes
- centralize pane registration for new right-pane view types
- keep `App.axaml` and shell template registration clean

Primary files:

- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/ShellPaneTemplateRegistry.cs`
- new controls under `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/`
- optionally `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/App.axaml`

Deliverables:

- new pane views can register cleanly without scattering template logic
- repeated right-pane section UI can be reused without inflating the route host

Dependencies:

- coordinate with B, C, and D as new panes land

Write boundary:

- Engineer F owns shared control extraction and template registration

## Suggested Execution Order

1. Finish Workstreams B and C so no standard route still requires `PclShellContentPanel`.
2. Land Workstream A cleanup immediately after the final route family is covered.
3. Run Workstream D on the largest remaining dedicated panes.
4. Run Workstream E to shrink shell-level state and compatibility shims.
5. Finish Workstream F cleanup and normalization.

## Merge Strategy

To keep engineers unblocked:

- B and C can work fully in parallel because setup and download/tools are separate route families.
- D can work in parallel because it only touches already-migrated dedicated panes.
- E should avoid editing route extraction files until B and C settle their pane ownership.
- F should accept small registration PRs from B, C, and D instead of making those engineers edit template files directly where possible.

Recommended branch discipline:

- one branch per workstream
- no cosmetic formatting-only changes across unrelated files
- no moving large XAML blocks and VM cleanup in the same PR unless the workstream explicitly owns both

## Milestones

### Milestone 1: Legacy Path Removal

Success criteria:

- no standard route renders through `LegacyStandardShellRightPaneView`
- `PclShellContentPanel.axaml` is out of the active standard-shell path

### Milestone 2: Large Dedicated Pane Cleanup

Success criteria:

- the top 5 to 6 largest active-path right panes are materially smaller
- repeated heavy sections are isolated cleanly

### Milestone 3: View-Model Fan-Out Reduction

Success criteria:

- route changes raise fewer pane-irrelevant properties
- compatibility booleans are reduced or removed where migration is complete

### Milestone 4: Performance Verification

Success criteria:

- startup and route transitions feel closer to `PCL.Neo`
- no left-pane misanimation regressions
- no overlap or clipped-pane regressions

## Acceptance Criteria

- standard-shell rendering no longer depends on `PclShellContentPanel`
- no active route keeps a hidden all-routes-live compatibility view around
- large dedicated panes are reduced where they remain performance hot spots
- shell animation remains host-level and visually consistent with the current CE shell
- no redesign drift from the `PCL.Neo`-aligned style
- build passes after each workstream

## Open Risks

- extracting too aggressively may create too many tiny controls with little actual performance benefit
- moving too much logic out of `FrontendShellViewModel` too early could destabilize command wiring
- a shared “family pane” can quietly become another mega-view if it accumulates too many subpage branches
- repeated list templates may become the next performance bottleneck after the legacy pane is removed

## Recommended First Tickets

1. Replace setup-family legacy routes with dedicated right-pane views.
2. Replace download-family legacy routes with dedicated right-pane views.
3. Replace remaining tools-family legacy routes with dedicated right-pane views.
4. Delete `LegacyStandardShellRightPaneView` active-path usage from `ResolveStandardRightPane`.
5. Split `InstanceSetupShellRightPaneView` into focused section controls.
6. Audit route-change property raises and remove migrated compatibility flags.

## Definition Of Done For Each Engineer

- route family or pane slice is no longer heavier than necessary
- no active rendering path depends on hidden legacy sections for that slice
- no change alters the visual style away from the current `PCL.Neo`-aligned shell
- `dotnet build /Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` passes
