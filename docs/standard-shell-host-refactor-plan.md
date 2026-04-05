# Standard Shell Host Refactor Plan

## Goal

Reduce shell weight during route changes by moving away from the single huge standard-shell content tree and toward Neo-style swapped hosts.

The end state should be:

- `MainWindow` owns fixed left and right shell slots.
- Standard-shell navigation swaps lightweight pane content into those slots.
- Route transitions animate only the shell hosts.
- The current all-surfaces-live approach in `PclShellContentPanel.axaml` is no longer on the active rendering path.
- Visual style remains aligned with `PCL.Neo`.

## Status Update

Last updated: 2026-04-05

Completed in the current slice:

- `MainWindow` standard-shell region now renders through swappable host content via `ContentControl`.
- `FrontendShellViewModel` now publishes a standard-shell pane contract:
  - `CurrentStandardLeftPane`
  - `CurrentStandardRightPane`
- `FrontendShellViewModel` now resolves every standard-shell route through a shared pane descriptor map:
  - exact right-pane kind
  - route-family group
  - compatibility-host flag for unmigrated views
- Pane VM contract files were added under `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/ShellPanes/`.
- Pane-to-view registration now lives centrally in `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/ShellPaneTemplateRegistry.cs`.
- `App` now registers shell pane templates through that dedicated registry instead of accumulating inline `Application.DataTemplates` entries in `App.axaml`.
- The standard-shell left host now resolves into dedicated pane variants:
  - navigation list pane
  - overview / facts pane
  - empty fallback pane
- The old monolithic left sidebar is no longer on the active standard-shell rendering path and the legacy `PclShellSidebarPanel` files were removed.
- The first dedicated right-pane extraction landed for `Tools > Help`.
- Dedicated right-pane extractions now cover the full instance family and the full version-saves family.
- Legacy `IsXxxSurface` properties now read from the resolved pane identity as temporary compatibility shims instead of duplicating route matching logic.
- Workstream G cleanup removed dead compatibility sections for:
  - `Tools > Help`
  - the full version-saves family
  - the full instance family
- Workstream G cleanup also removed the corresponding migrated-family `IsXxxSurface` properties and `RaisePropertyChanged(nameof(IsXxxSurface))` calls.
- Remaining standard-shell right routes still use a temporary compatibility pane that hosts a reduced `PclShellContentPanel` for the not-yet-extracted setup/download/tools routes.
- `dotnet build PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` passes after the host-contract changes.

Current migration state:

- Workstream A: substantially landed for standard-shell hosts.
- Workstream B: route-to-pane-resolution contract landed for all standard-shell routes; legacy surface flags are now compatibility shims over the resolved pane descriptor.
- Workstream C: centralized pane template registry landed; downstream pane extractions can add mappings in one file without touching `App.axaml`.
- Workstream D: still in progress.
- Workstream F: completed for standard-shell left-pane decomposition.
- Workstream E: instance/version-saves right-pane extraction landed.
- Workstream G: in progress; migrated-family compatibility cleanup landed, remaining removal work is blocked on the unfinished setup/download/tools extractions from Workstream D.

## Reference Files

These are the primary implementation references from `PCL.Neo`:

- `/Users/theunknownthing/PCL.Neo/PCL.Neo/Views/MainWindow.axaml`
- `/Users/theunknownthing/PCL.Neo/PCL.Neo/Views/MainWindow.axaml.cs`

These are the primary CE files to refactor:

- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/MainWindow.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/MainWindow.axaml.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclLaunchLeftPanel.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclLaunchRightPanel.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Navigation.cs`

## Why This Refactor Is Needed

Current CE behavior is still architecturally heavy:

- `PclShellContentPanel.axaml` is a 5,000+ line permanent right-side tree.
- It contains 30+ route surfaces behind `IsVisible` toggles.
- `FrontendShellViewModel` exposes many `IsXxxSurface` booleans.
- Navigation refresh raises a large number of surface properties on every route change.
- Animation performance is limited because the host is animating a very large live visual tree.

`PCL.Neo` is lighter because it swaps host content rather than keeping every route surface alive.

## Target Architecture

### Main shell model

`MainWindow` should own stable host slots:

- launch left host
- launch right host
- standard left host
- standard right host

The standard shell should render through swappable host content:

- current left pane view model
- current right pane view model

### Route resolution model

A single route should resolve to:

- one left pane
- one right pane
- shell metadata
- animation direction

This replaces the current model of many route booleans such as:

- `IsSetupLaunchSurface`
- `IsDownloadCatalogSurface`
- `IsDownloadResourceSurface`
- `IsInstanceSetupSurface`
- `IsVersionSaveInfoSurface`
- `IsGenericShellSurface`

### View composition model

Each route family should render through a small dedicated pane view, not through one mega-view.

Examples:

- setup family pane views
- download install pane
- download catalog pane
- download resource pane
- tools pane views
- instance pane views
- version saves pane views

## Parallel Workstreams

The work can and should be done concurrently, but the interfaces below must be agreed first.

### Workstream A: Shell Host Infrastructure

Owner:
- Senior Avalonia / shell engineer

Scope:
- Update `MainWindow` standard-shell region to host swappable content.
- Introduce `ContentControl` or equivalent host-based rendering for standard left and right panes.
- Keep launch shell behavior intact.
- Preserve host-level animation entry points in `MainWindow.axaml.cs`.

Files:
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/MainWindow.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/MainWindow.axaml.cs`

Deliverables:
- Standard shell hosts no longer directly hardwire `PclShellSidebarPanel` and `PclShellContentPanel`.
- Hosts can bind to current left and right pane content.
- Existing animation can target the new hosts without changing style.

Dependencies:
- Starts first.
- Must define host contract before pane extraction teams land work.

### Workstream B: Navigation Contract and Pane Resolution

Owner:
- ViewModel / architecture engineer

Scope:
- Replace surface-boolean-driven routing with a route-to-pane-resolution model.
- Extend `RefreshShell()` to compute the current left pane and current right pane.
- Introduce pane view model types or pane descriptors.
- Preserve current commands and collections for reuse.

Files:
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Navigation.cs`
- New files under `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/ShellPanes/`

Deliverables:
- `FrontendShellViewModel` exposes current pane identities or pane VMs.
- `MainWindow` can bind to those pane outputs.
- Existing route booleans are either reduced or wrapped behind temporary compatibility shims.

Dependencies:
- Starts first with Workstream A.
- Must publish pane contract before most view extraction work merges.

### Workstream C: View Registration and Template Mapping

Owner:
- Avalonia engineer

Scope:
- Centralize mapping from pane VM type to pane view.
- Prefer DataTemplates or a dedicated registry over hardcoded XAML route switches.

Files:
- New registry or template files under `PCL.Frontend.Spike/Desktop/`
- Possibly `App.axaml` if templates are registered globally

Deliverables:
- Standard left and right hosts can render pane VMs cleanly.
- Mapping lives in one place.

Dependencies:
- Depends on Workstream B pane contract.
- Can proceed in parallel with D and E once pane base types exist.

### Workstream D: Setup / Download / Tools Pane Extraction

Owner:
- Engineer D

Scope:
- Extract setup, download, and tools views out of the current giant `PclShellContentPanel`.
- Group by route family where layout is shared.
- Reuse existing commands and collections from partial VM files.

Suggested families:
- setup family
- download install
- download catalog
- download resource
- download favorites
- tools family

Files:
- Source:
  - `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`
  - `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Setup*.cs`
  - `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Download*.cs`
  - `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Tool*.cs`
- Target:
  - `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/`

Deliverables:
- Dedicated right-pane views for setup/download/tools.
- No visual redesign.
- Existing logic reused, not rewritten unnecessarily.

Dependencies:
- Depends on Workstream B pane contract.
- Can proceed in parallel with Workstream E.

### Workstream E: Instance / VersionSaves Pane Extraction

Owner:
- Engineer E

Scope:
- Extract instance and version-saves routes from `PclShellContentPanel`.
- Preserve existing behavior and commands.

Suggested families:
- instance overview/setup/export/install
- instance world/screenshot/server/resource
- version saves info/backup/datapack

Files:
- Source:
  - `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`
  - `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Instance*.cs`
  - `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.VersionSaves.cs`
- Target:
  - `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/`

Deliverables:
- Dedicated right-pane views for instance and version-saves routes.
- Existing behavior preserved.

Dependencies:
- Depends on Workstream B pane contract.
- Can proceed in parallel with Workstream D.

### Workstream F: Left Pane Decomposition

Owner:
- Engineer focused on sidebar and supporting panes

Scope:
- Split the current standard left-side content into smaller pane views.
- Preserve launch left pane for the first phase.

Likely left pane types:
- navigation list pane
- overview / facts pane
- empty / fallback pane

Files:
- Source:
  - `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclShellSidebarPanel.axaml`
- Target:
  - `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Left/`

Deliverables:
- Standard shell left side is rendered via pane host.
- Old monolithic sidebar panel is no longer on the active path.

Dependencies:
- Depends on Workstream B pane contract.
- Can proceed in parallel with D and E.

### Workstream G: Legacy Removal and Compatibility Cleanup

Owner:
- Cleanup / integration engineer

Scope:
- Remove obsolete boolean surface properties.
- Remove obsolete `RaisePropertyChanged(nameof(IsXxxSurface))` calls.
- Delete old standard-shell monoliths once inactive.
- Remove temporary compatibility shims after all pane routes are migrated.

Files:
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Navigation.cs`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclShellContentPanel.axaml`
- `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/Controls/PclShellSidebarPanel.axaml`

Deliverables:
- Obsolete route boolean model removed or drastically reduced.
- Giant standard-shell content panel removed from active rendering path.

Dependencies:
- Last major workstream.
- Starts after D, E, and F are merged.

## Concurrency Rules

To avoid conflicts, engineers should not edit the same write surfaces at the same time.

### Shared contract freeze

Before D, E, and F begin:

- Workstream A and B must agree on:
  - pane host type
  - pane VM base type or pane descriptor type
  - registration strategy
  - naming convention

This contract should be published in code first.

### Suggested write boundaries

Engineer A:
- `MainWindow.axaml`
- `MainWindow.axaml.cs`

Engineer B:
- `FrontendShellViewModel.cs`
- `FrontendShellViewModel.Navigation.cs`
- new pane VM contract files

Engineer C:
- shared shell coordination / cross-stream review only

Engineer D:
- new right-pane setup/download/tools view files
- relevant setup/download/tools VM partials only if absolutely necessary

Engineer E:
- new right-pane instance/version-saves view files
- relevant instance/version-saves VM partials only if absolutely necessary

Engineer F:
- new left-pane view files
- sidebar-specific VM support if needed

Engineer G:
- deletes obsolete code after the new path is proven

## Milestones

### Milestone 1: Host Contract Lands

Success criteria:
- `MainWindow` can bind to current standard left and right pane content.
- One route family renders through the new host path.
- Existing animation still works.

Status:
- Completed on 2026-04-05.
- Landed with a compatibility fallback for non-migrated right-pane routes.

### Milestone 2: Standard Shell Swapped Hosts

Success criteria:
- All standard-shell routes render via pane hosts.
- `PclShellContentPanel.axaml` is no longer used for active standard-shell rendering.

### Milestone 3: Legacy Surface Model Removed

Success criteria:
- Most or all `IsXxxSurface` properties are removed.
- `RaiseShellStateProperties()` no longer raises dozens of route visibility properties.

### Milestone 4: Performance Polish

Success criteria:
- Route change cost drops because only active pane views are live.
- Startup and host animations feel smoother than the current giant-tree approach.

## Risks

### Risk 1: Business logic and route visibility are currently intertwined

Evidence:
- Many partial VM files check `IsDownloadResourceSurface`, `IsInstanceInstallSurface`, etc.

Mitigation:
- Introduce temporary compatibility helpers during migration.
- Remove them only after pane routing is stable.

### Risk 2: Over-fragmentation

If engineers create one file per exact route without grouping by family, the shell may become hard to maintain.

Mitigation:
- Group by layout family first.
- Split further only when shared layouts become too conditional.

### Risk 3: Merge conflicts in navigation code

Mitigation:
- Keep Workstream B small and authoritative.
- Downstream teams consume its contract instead of reinventing routing logic locally.

### Risk 4: Style drift

Mitigation:
- Engineers must extract existing markup, not redesign it.
- `PCL.Neo` reference files above are visual and structural references.

## Acceptance Criteria

The refactor is complete when:

- Standard shell no longer depends on the all-surfaces-live model in `PclShellContentPanel.axaml`.
- Standard shell renders through swapped left and right hosts in `MainWindow`.
- Host-level animations remain intact and do not overlap panes.
- No visual redesign is introduced.
- Existing commands, collections, and route behavior still work.
- Build passes and route changes feel lighter than today.

## Recommended Next Step

Continue the migration by replacing the temporary right-pane compatibility path family-by-family:

- extract setup/download/tools routes from `PclShellContentPanel.axaml` into `/Users/theunknownthing/PCL-CE/PCL.Frontend.Spike/Desktop/ShellViews/Right/`
- extract instance/version-saves routes into the same host system
- introduce additional dedicated left-pane variants only where the shared sidebar/overview pane becomes too conditional
- remove compatibility shims and obsolete `IsXxxSurface` booleans only after all active standard-shell routes are off the monolith
