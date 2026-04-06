# Incomplete Backend Wiring Audit

Date: 2026-04-06

## Scope

This audit targets the portable frontend shell in `PCL.Frontend.Spike`.

I treated a page as incomplete when at least one of these was true:

- the route is explicitly described as a migration scaffold or sample-backed
- the page falls back to a generic migration surface instead of a route-specific contract
- the page substitutes local fixtures, local files, or review artifacts for the real backend data source
- the page keeps the layout but still records an intent or writes a report file instead of invoking the real runtime/backend path

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
