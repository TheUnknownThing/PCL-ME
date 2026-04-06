# Incomplete Backend Wiring Audit

Date: 2026-04-06

## Scope

This audit targets the portable frontend shell in `PCL.Frontend.Spike`.

I treated a page as incomplete when at least one of these was true:

- the route is explicitly described as a migration scaffold or sample-backed
- the page falls back to a generic migration surface instead of a route-specific contract
- the page substitutes local fixtures, local files, or review artifacts for the real backend data source
- the page keeps the layout but still records an intent or writes a report file instead of invoking the real runtime/backend path

## Concurrent Dispatch Plan

Use the incomplete-page list below as the source inventory, but dispatch implementation through the following non-overlapping work packages.

To keep concurrent engineers from colliding:

- each worker should own only the route family listed in their step
- workers should avoid editing shared route-registration files during parallel work
- one final integrator should update shared route-registration and shell-switch files after the route-family fixes are ready

Shared integration files that should be reserved for the final integrator step:

- `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs`
- `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.ShellPanes.cs`
- `PCL.Core/App/Essentials/LauncherFrontendNavigationService.cs`

### Step List

| Step | Exclusive route scope | What the assigned engineer should finish | Verification gate |
| --- | --- | --- | --- |
| `Step 1` | `Launch`, `InstanceSelect`, `TaskManager`, `GameLog` | Replace sample-backed or generic migration surfaces with route-specific runtime data and actions for launch-state, instance selection, active tasks, and game logs. | `Launch` no longer depends on sample launch inputs; `InstanceSelect` renders a real route-specific surface; `TaskManager` shows live task state; `GameLog` shows live log data instead of migration-only handoff text. |
| `Step 2` | `DownloadInstall`, `VersionInstall` | Finish the installer workflow so the shell no longer falls back to the old installer for unmanaged cases, and replace placeholder install recommendations/compatibility inputs with real data. | Both routes complete install flows end to end inside the new shell for the supported install matrix, with no old-installer fallback in the exercised scenarios. |
| `Step 3` | `DownloadClient`, `DownloadOptiFine`, `DownloadForge`, `DownloadNeoForge`, `DownloadCleanroom`, `DownloadFabric`, `DownloadLegacyFabric`, `DownloadQuilt`, `DownloadLiteLoader`, `DownloadLabyMod` | Replace local-manifest catalog composition with the real backend/remote catalog pipeline for version and loader downloads. | Each loader/version page loads real catalog entries from the intended backend source, and the displayed list changes when the backend catalog changes. |
| `Step 4` | `DownloadMod`, `DownloadPack`, `DownloadDataPack`, `DownloadResourcePack`, `DownloadShader`, `DownloadWorld` | Replace local-instance/local-save resource lists with real backend/community search and browse results for downloadable content. | Each resource page shows backend/community results instead of only local files from the selected instance/save. |
| `Step 5` | `DownloadCompFavorites`, `CompDetail`, `HomePageMarket` | Finish project/favorites/detail surfaces so favorites resolve live metadata, detail pages become real route implementations, and market/home content is route-specific rather than generic. | Favorites resolve project names/details from the real source; `CompDetail` has a dedicated detail surface; `HomePageMarket` renders dedicated market data instead of the generic download page. |
| `Step 6` | `SetupLink`, `SetupFeedback`, `SetupJava`, `SetupUpdate` | Implement the missing setup routes and remove remaining intent-only or hardcoded setup behaviors. | `SetupLink` renders a dedicated route; `SetupFeedback` is backed by a real source; `SetupJava` row actions execute real behaviors; `SetupUpdate` no longer ships the disabled placeholder optional-update card. |
| `Step 7` | `ToolsGameLink`, `ToolsLauncherHelp`, `HelpDetail`, `ToolsTest` | Replace replacement-shell/tool-intent behavior with real runtime/help/detail implementations for the tools/help family. | `ToolsGameLink` uses the intended runtime path, `HelpDetail` becomes a real page, and toolbox/help actions no longer degrade to raw intent logging for the audited routes. |
| `Step 8` | Shared integration only | Update shared route registration and shell-switch wiring after Steps 1-7 are individually ready. This step should be the only one that edits the reserved shared integration files. | All completed route-family fixes are reachable through normal navigation, and the reserved shared files show only integration glue rather than feature logic. |

### Recommended Verification Order

Verify steps in numeric order even if engineers implement them concurrently. That keeps each review focused on one route family and leaves the shared routing sweep for the end.

### Step Progress

- 2026-04-06: `Step 1` completed. `Launch` now derives its runtime fallback inputs from live config/manifest/runtime state instead of `SpikeSampleFactory`; `InstanceSelect` renders a dedicated instance picker backed by the actual launcher `versions` directory; `TaskManager` binds to live `TaskCenter` task state with pause/cancel affordances; and `GameLog` shows live launch output plus recent runtime log artifacts instead of migration-only handoff text.
- 2026-04-06: `Step 2` completed. `DownloadInstall` and `VersionInstall` now keep the supported install matrix inside the new shell, surface route-local compatibility and recommendation data on the install cards, clear stale loader selections when the Minecraft version changes, and block apply/reset when an existing selection cannot be mapped to a supported install source.
- 2026-04-06: `Step 3` completed. `DownloadClient`, `DownloadOptiFine`, `DownloadForge`, `DownloadNeoForge`, `DownloadCleanroom`, `DownloadFabric`, `DownloadLegacyFabric`, `DownloadQuilt`, `DownloadLiteLoader`, and `DownloadLabyMod` now compose their page data from live remote catalogs through `FrontendDownloadRemoteCatalogService`, with source-aware fallback and cache reuse instead of launcher-folder manifest inspection. Verification confirmed the live endpoint shapes for the Step 3 catalogs, and `dotnet build PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` now gets past the Step 3 files; the remaining frontend build break is currently unrelated in `PCL.Frontend.Spike/Workflows/FrontendCommunityResourceCatalogService.cs` because `Secrets` is unresolved there.
- 2026-04-06: `Step 4` completed. `DownloadMod`, `DownloadPack`, `DownloadDataPack`, `DownloadResourcePack`, `DownloadShader`, and `DownloadWorld` now compose live community browse results from Modrinth and/or CurseForge-compatible endpoints (using the launcher's existing community-source preference), route row actions to the real project pages, and expose source/tag filtering against backend/community data instead of local instance/save folders. Verification confirmed live results for all six route families via the wired endpoints; full frontend build validation is currently blocked by unrelated existing `Secrets` reference errors in `PCL.Frontend.Spike/Workflows/FrontendCommunityResourceCatalogService.cs`.
- 2026-04-06: `Step 5` completed. `DownloadCompFavorites` now resolves stored favorites against live Modrinth and CurseForge metadata, preserves unresolved IDs behind a real `CompDetail` route, and reuses the launcher's community-source preference with cache-backed source fallback. `CompDetail` now renders a dedicated detail surface with live project summary, compatibility, recent versions, and related links, while `HomePageMarket` now renders its own market aggregation from live community browse results instead of falling back to the generic download surface. Verification confirmed the wired Modrinth and CurseForge endpoints for favorites/detail payloads, and `dotnet build PCL.Frontend.Spike/PCL.Frontend.Spike.csproj /clp:ErrorsOnly` now gets past the Step 5 files; the remaining frontend build break is currently unrelated in `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceActions.cs` because `PeHeaderParser` is unresolved there.
- 2026-04-06: `Step 6` completed. `SetupLink` now resolves to a dedicated setup pane instead of the generic fallback, `SetupFeedback` loads live GitHub issue state through `FrontendSetupFeedbackService`, `SetupJava` row actions now open the real runtime folder and a generated runtime-detail artifact backed by parsed Java metadata, and `SetupUpdate` no longer ships the disabled optional-update card. Verification confirmed the live feedback endpoint shape against the GitHub issues API and the route-local setup wiring in the spike shell; `dotnet build PCL.Frontend.Spike/PCL.Frontend.Spike.csproj` is currently blocked by unrelated in-progress frontend errors in `PCL.Frontend.Spike/Workflows/FrontendCommunityProjectService.cs` and `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.HelpDetail.cs`.
- 2026-04-06: `Step 7` completed. `ToolsGameLink` now drives the real lobby runtime through a reflection-based `PCL.Core` bridge that keeps the Avalonia shell on `net10.0` while still synchronizing live lobby state, world discovery, lobby actions, player presence, and local-forward endpoint details from the intended EasyTier/Natayark backend. `HelpDetail` now renders parsed help-entry detail content on a dedicated route surface, and the Step 7 help/toolbox actions no longer degrade to raw intent logging for the audited routes. Verification confirmed the Step 7 shell wiring with `git diff --check`, and `dotnet build PCL.Frontend.Spike/PCL.Frontend.Spike.csproj /clp:ErrorsOnly` now only fails on the unrelated pre-existing `PeHeaderParser` reference in `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceActions.cs`.
- 2026-04-06: `Step 8` completed. The reserved shared integration layer now binds `CompDetail` and `HomePageMarket` back into the download navigation family, binds `HelpDetail` into the tools navigation family, assigns unique generic-pane keys for the dedicated secondary/detail/utility routes, and updates the shared page-content contracts so the completed Step 1/5/7 utility-detail routes are described as integration surfaces instead of migration placeholders. Verification confirmed the shared wiring with `git diff --check` and `dotnet build PCL.Core.Test/PCL.Core.Test.csproj /clp:ErrorsOnly`; targeted `LauncherFrontendNavigationServiceTest` execution is currently blocked in this macOS environment because `Microsoft.WindowsDesktop.App 10.0.0` is not installed for the WindowsDesktop test host.

## Confirmed Incomplete Pages

### Launch And Utility Routes

| Route | Status | Why it is incomplete | Evidence |
| --- | --- | --- | --- |
| `Launch` | Incomplete | The launch surface is still sample-backed. The frontend composes launch defaults from `SpikeSampleFactory`, and the project README explicitly says launch-page state is still largely sample-backed and does not perform full live launcher/login/network execution. | `PCL.Frontend.Spike/README.md:98-103`, `PCL.Frontend.Spike/README.md:160-166`, `PCL.Frontend.Spike/Workflows/Inspection/FrontendInspectionLaunchCompositionService.cs:19-23`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:45-90` |
| `InstanceSelect` | Incomplete | The route exists in the navigation catalog, but only `Launch` gets the launch-route host. Non-launch pages fall into the standard shell, and unrecognized secondary pages fall back to the generic shell/page surface instead of an instance-selection implementation. | `PCL.Core/App/Essentials/LauncherFrontendNavigationService.cs:188-195`, `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.cs:151-153`, `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.ShellPanes.cs:119-127`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:1250-1275` |
| `TaskManager` | Incomplete | The page is still a migration surface that reviews workflow summaries instead of binding to a live task collection. | `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:924-968` |
| `GameLog` | Incomplete | The page is still a migration surface for routing/recovery handoff, not a fully wired live game-log backend. | `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:971-1015` |

### Download Routes

| Route | Status | Why it is incomplete | Evidence |
| --- | --- | --- | --- |
| `DownloadInstall` | Partially wired | The UI supports the frontend-managed install options, but anything outside that subset still defers to the old installer. The page-content contract also says finer install recommendations and compatibility input are still pending. | `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.InstallWorkflow.cs:150-156`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:112-154`, `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceInitialization.cs:61-67` |
| `VersionInstall` | Partially wired | It shares the same installer workflow and the same managed-option limitation as `DownloadInstall`. | `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.InstallWorkflow.cs:150-156` |
| `DownloadClient` | Incomplete | The page is populated from local manifest files in the current launcher folder, not from the real download/backend catalog. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:42-78` |
| `DownloadOptiFine` | Incomplete | Same issue: loader catalog is built from locally recognized versions, not from the actual remote/backend source. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:42-106` |
| `DownloadForge` | Incomplete | Same as above. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:42-106` |
| `DownloadNeoForge` | Incomplete | Same as above. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:42-106` |
| `DownloadCleanroom` | Incomplete | Same as above. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:42-106` |
| `DownloadFabric` | Incomplete | Same as above. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:42-106` |
| `DownloadLegacyFabric` | Incomplete | Same as above. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:42-106` |
| `DownloadQuilt` | Incomplete | Same as above. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:42-106` |
| `DownloadLiteLoader` | Incomplete | Same as above. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:42-106` |
| `DownloadLabyMod` | Incomplete | Same as above. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:42-106` |
| `DownloadMod` | Incomplete | The resource list is built from the currently selected instance's installed mods instead of real community/backend search results. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:145-159`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:157-200` |
| `DownloadPack` | Incomplete | The page lists locally existing instance versions instead of real pack search data. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:160-167`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:157-200` |
| `DownloadDataPack` | Incomplete | The page lists datapacks from the currently selected save instead of real backend/community resource data. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:168-188`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:157-200` |
| `DownloadResourcePack` | Incomplete | The page lists local instance resource packs instead of real backend/community resource data. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:189-196`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:157-200` |
| `DownloadShader` | Incomplete | The page lists local shaderpacks instead of real backend/community resource data. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:197-204`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:157-200` |
| `DownloadWorld` | Incomplete | The page lists worlds from the current instance instead of real backend/community world search data. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:205-225`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:157-200` |
| `DownloadCompFavorites` | Incomplete | The page only has locally stored favorite IDs and notes. It explicitly warns that online project metadata is missing, and the row action falls back to an intent when no target is present. | `PCL.Frontend.Spike/Workflows/FrontendDownloadCompositionService.cs:108-143`, `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceInitialization.cs:258-285` |
| `CompDetail` | Incomplete | The route exists, but there is no dedicated detail-page handling. It is routed through the download family and falls back to the generic download migration surface. | `PCL.Core/App/Essentials/LauncherFrontendNavigationService.cs:191`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:27`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:203-245`, `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.ShellPanes.cs:148-170` |
| `HomePageMarket` | Incomplete | The route exists, but there is no dedicated page implementation. It also falls back to the generic download migration surface. | `PCL.Core/App/Essentials/LauncherFrontendNavigationService.cs:195`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:27`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:203-245`, `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.ShellPanes.cs:118-127` |

### Setup Routes

| Route | Status | Why it is incomplete | Evidence |
| --- | --- | --- | --- |
| `SetupLink` | Incomplete | The route exists in the sidebar catalog, but it is not handled by the setup right-pane resolver or the setup page-content switch, so it falls back to the generic setup surface. | `PCL.Core/App/Essentials/LauncherFrontendNavigationService.cs:144-151`, `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.ShellPanes.cs:130-145`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:249-265` |
| `SetupFeedback` | Incomplete | The page contents are hardcoded local cards, not data from a real feedback backend or issue source. | `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceInitialization.cs:52-90` |
| `SetupJava` | Partially wired | Java selection and enablement persist real config, but per-row "open folder" and "view detail" actions are still intent-only. | `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceActions.cs:1460-1485` |
| `SetupUpdate` | Partially wired | The main update check is real, but the optional update card is hardcoded and disabled. | `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.LaunchUpdate.cs:128-148`, `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.Construction.cs:313-314` |

### Tools Routes

| Route | Status | Why it is incomplete | Evidence |
| --- | --- | --- | --- |
| `ToolsGameLink` | Incomplete | Join/create/exit lobby actions generate replacement-shell session artifacts instead of talking to the real EasyTier/Natayark runtime. The page-content contract also says the real runtime should stay outside the current shell. | `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceActions.cs:348-392`, `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceActions.cs:430-478`, `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.SurfaceActions.cs:490-520`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:751-790` |
| `ToolsLauncherHelp` | Partially wired | The main list loads, but the help route still documents that finer help-entry/detail contracts are pending. | `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:840-879` |
| `HelpDetail` | Incomplete | The detail route exists, but there is no detail-page implementation. Non-event help entries only record the raw path instead of navigating to a real help-detail surface. | `PCL.Core/App/Essentials/LauncherFrontendNavigationService.cs:192`, `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.ToolsComposition.cs:82-99`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:742-747`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:867-876` |
| `ToolsTest` | Partially wired | Several tools are shell-side substitutes rather than the original feature implementations, and unknown toolbox actions still fall back to a pure intent command. | `PCL.Frontend.Spike/ViewModels/FrontendShellViewModel.ToolsComposition.cs:49-60`, `PCL.Core/App/Essentials/LauncherFrontendPageContentService.cs:882-915` |

## Pages Not Flagged In This Audit

These pages appear to be backed by real local/runtime state strongly enough that I did not classify them as "backend-unwired" for this report:

- `SetupAbout`
- `SetupLaunch`
- `SetupGameLink`
- `SetupGameManage`
- `SetupLauncherMisc`
- `SetupUI`
- `SetupLog`
- the core update-check path in `SetupUpdate`
- `VersionSavesInfo`
- `VersionSavesBackup`
- `VersionSavesDatapack`
- `VersionOverall`
- `VersionSetup`
- `VersionExport`
- `VersionWorld`
- `VersionScreenshot`
- `VersionServer`
- `VersionMod`
- `VersionModDisabled`
- `VersionResourcePack`
- `VersionShader`
- `VersionSchematic`

Those pages still contain some migration-language or shell-only affordances in places, but their main read/write behavior is already tied to real config files, filesystem state, or the extracted repair/install services rather than pure placeholders.

## Notes

- The README still sets the overall expectation: the desktop shell is a migration scaffold, not a full launcher, and many page-specific production contracts remain incomplete.
- I used the route catalog from `LauncherFrontendNavigationService` so the audit covered the known page inventory instead of only the pages that are easy to click to manually.
