# PCL-CE Portability Handoff

## Project Context

`PCL-CE` is a Windows-first Minecraft launcher with:

- a WPF/VB frontend in `Plain Craft Launcher 2`
- a mixed C# runtime/core in `PCL.Core`

The long-term direction is still:

1. make the runtime/core portable first
2. keep current Windows launcher behavior working through adapters
3. replace the WPF/VB frontend only after the runtime boundary is stable

The repo is no longer at the “portability is just an idea” stage. There is now a real headless `PCL.Core.Foundation` project, it builds and tests on macOS, Wave 2 runtime/platform extraction is complete, and a substantial set of Wave 3 launcher-workflow cleanup slices have been landed.

Latest continuation update:

- Microsoft device-code response parsing and popup prompt planning now live in `PCL.Core.Minecraft.Launch.MinecraftLaunchMicrosoftDeviceCodePromptService`
- launcher Microsoft device-code login prompt construction in `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` and `Plain Craft Launcher 2/Pages/PageLaunch/MyMsgLogin.xaml.vb` now route through `MinecraftLaunchMicrosoftDeviceCodePromptService` instead of consuming raw OAuth JSON directly
- portable Java runtime default ignored-hash policy, runtime base-directory selection, and download state transition cleanup/refresh planning now live in `PCL.Core.Minecraft.Launch.MinecraftJavaRuntimeDownloadSessionService`
- launcher Java runtime download lifecycle cleanup / refresh handling in `Plain Craft Launcher 2/Modules/Minecraft/ModJava.vb` now routes through `MinecraftJavaRuntimeDownloadSessionService`
- launcher Java runtime download lifecycle cleanup / refresh shell application is now centralized in `Plain Craft Launcher 2/Modules/Minecraft/ModJavaDownloadSessionShell.vb`
- launcher Microsoft live HTTP request execution is now centralized in `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchMicrosoftRequestShell.vb`
- launcher Authlib live HTTP request / metadata execution is now centralized in `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchAuthlibRequestShell.vb`
- `PCL.Frontend.Spike` now sources Java runtime base-directory selection from `MinecraftJavaRuntimeDownloadSessionService` instead of duplicating that path policy locally
- `PCL.Frontend.Spike` can now model finished / failed / aborted Java download session transitions, including cleanup / refresh artifacts, from `MinecraftJavaRuntimeDownloadSessionService`
- portable Java runtime transfer selection and reused-file filtering now live in `PCL.Core.Minecraft.Launch.MinecraftJavaRuntimeDownloadWorkflowService`
- launcher Java download candidate filtering in `Plain Craft Launcher 2/Modules/Minecraft/ModJava.vb` now routes through `MinecraftJavaRuntimeDownloadWorkflowService`
- `PCL.Frontend.Spike` now distinguishes reused Java runtime files from actual transfer files, writes a dedicated transfer artifact, and can detect best-effort existing runtime files in `--host-env true` mode
- crash export save-dialog title / default filename / filter planning now live in `PCL.Core.Minecraft.MinecraftCrashResponseWorkflowService`
- launch script-export completion log / abort hint / reveal-target policy now live in `PCL.Core.Minecraft.Launch.MinecraftLaunchShellService`
- launch custom-command and game-process `ProcessStartRequest` construction plus priority application now live in `PCL.Core.Minecraft.Launch.MinecraftLaunchProcessExecutionService`
- shell-spike launch scenarios now model Authlib / Microsoft login request execution with inspectable request / response artifacts instead of only high-level prompt transcripts
- shell-spike execute mode now supports explicit crash-export destination handoff and records the selected archive target as a shell artifact
- shell-spike can now derive best-effort host-backed startup / launch / crash inputs from the current machine with `--host-env true`
- portable Java runtime selection and manifest file planning now live in `PCL.Core.Minecraft.Launch.MinecraftJavaRuntimeDownloadService`
- portable Java runtime manifest/file request coordination now lives in `PCL.Core.Minecraft.Launch.MinecraftJavaRuntimeDownloadWorkflowService`
- portable Java launch selection transition/log/hint policy now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchJavaSelectionWorkflowService`
- launcher Java runtime manifest selection / file planning in `Plain Craft Launcher 2/Modules/Minecraft/ModJava.vb` now route through `MinecraftJavaRuntimeDownloadService`
- launcher Java runtime index / manifest / file source shaping in `Plain Craft Launcher 2/Modules/Minecraft/ModJava.vb` now routes through `MinecraftJavaRuntimeDownloadWorkflowService`
- launcher Java selection / prompt / download retry orchestration in `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` is now funneled through `ModJava.vb` consuming `MinecraftLaunchJavaSelectionWorkflowService`
- portable Java runtime download planning now preserves Windows-style runtime paths even when the backend is exercised on non-Windows hosts
- a new portable `net8.0` backend assembly now lives in `PCL.Core.Backend`, compiling the extracted startup / launch / crash workflow layer outside `PCL.Core`'s `net8.0-windows` target
- a new portable backend test lane now lives in `PCL.Core.Backend.Test`, and its extracted workflow/service coverage runs on macOS/Linux hosts
- `PCL.Frontend.Spike` now targets plain `net8.0` and runs against `PCL.Core.Backend` instead of `PCL.Core`
- `PCL.Core` now references `PCL.Core.Backend` and no longer compiles that extracted workflow slice directly, making the portable backend assembly the source of truth for those services
- launch Java requirement / missing-Java recovery workflow now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchJavaWorkflowService`
- a thin replacement-shell spike now lives in `PCL.Frontend.Spike`, can exercise extracted startup / launch / crash services without WPF page code, and now exposes `plan` / `run` / `execute` CLI modes with JSON payloads, text-mode shell transcripts, workspace artifact materialization, and file-backed input replay
- startup visual shell defaults now live in `PCL.Core.App.Essentials.LauncherStartupVisualService`
- launcher startup open-count milestone policy now lives in `PCL.Core.App.Essentials.LauncherStartupMilestoneService`
- launcher startup update-log prompt policy now lives in `PCL.Core.App.Essentials.LauncherUpdateLogService`
- launch prerun options-file mutation policy now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchOptionsFileService`
- launch prerun composition for GPU recovery, `launcher_profiles.json`, and `options.txt` now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchPrerunWorkflowService`
- launch prerun `launcher_profiles.json` mutation policy now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchLauncherProfilesService`
- launcher_profiles default file seeding now lives in `PCL.Core.Minecraft.MinecraftLauncherProfilesFileService`
- launch prerun `launcher_profiles.json` retry / reset workflow now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchLauncherProfilesWorkflowService`
- launch-session music / video-background / launcher-visibility shell policy now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchShellService`
- launch custom-command / batch-script planning now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchCustomCommandService`
- launch-session startup summary logging now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchSessionLogService`
- launch process / watcher runtime planning now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchRuntimeService`
- launch custom-command / game-process shell execution planning now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchExecutionWorkflowService`
- launch watcher startup composition now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchWatcherWorkflowService`
- launch-session start/post-launch shell composition now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchSessionWorkflowService`
- Authlib request / response protocol shaping now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchAuthlibProtocolService`
- Authlib response planning for profile selection, refresh-session resolution, and profile mutation now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchAuthlibLoginWorkflowService`
- third-party/Authlib login failure transition policy now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchThirdPartyLoginWorkflowService`
- Microsoft request / response protocol shaping now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchMicrosoftProtocolService`
- Authlib request URL / header / body planning now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchAuthlibRequestWorkflowService`
- Microsoft login request URL / header / body / bearer-token planning now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchMicrosoftRequestWorkflowService`
- Microsoft login refresh / XSTS / ownership / profile failure policy now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchMicrosoftFailureWorkflowService`
- launch argument window-size planning now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchResolutionService`
- launch argument final composition, placeholder application, and quick-play/server append policy now live in `PCL.Core.Minecraft.Launch.MinecraftLaunchArgumentWorkflowService`
- launch classpath ordering now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchClasspathService`
- launch placeholder/replacement value assembly now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchReplacementValueService`
- launch natives-directory selection now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchNativesDirectoryService`
- launch natives archive sync now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchNativesSyncService`
- launch RetroWrapper selection policy now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchRetroWrapperService`
- launch JSON argument-section extraction now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchJsonArgumentService`
- launch JVM argument assembly now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchJvmArgumentService`
- launch game argument assembly now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchGameArgumentService`
- startup version-transition policy now lives in `PCL.Core.App.Essentials.LauncherVersionTransitionService`
- startup version-transition application planning now lives in `PCL.Core.App.Essentials.LauncherVersionTransitionWorkflowService`
- startup version-isolation migration policy now lives in `PCL.Core.App.Essentials.LauncherStartupVersionIsolationMigrationService`
- crash-export request assembly now lives in `PCL.Core.Minecraft.MinecraftCrashExportWorkflowService`
- crash-export archive creation now lives in `PCL.Core.Minecraft.MinecraftCrashExportArchiveService`
- crash prompt response / export completion shell policy now lives in `PCL.Core.Minecraft.MinecraftCrashResponseWorkflowService`
- launch-count support prompt policy now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchShellService`
- prelaunch GPU-preference failure / admin-retry policy now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchGpuPreferenceWorkflowService`
- startup immediate-command shell policy and environment-warning prompt contract now live in `PCL.Core.App.Essentials.LauncherStartupShellService`
- startup shell composition now lives in `PCL.Core.App.Essentials.LauncherStartupWorkflowService`
- main-window startup composition now lives in `PCL.Core.App.Essentials.LauncherMainWindowStartupWorkflowService`
- launcher startup visual application now routes through `Plain Craft Launcher 2/Application.xaml.vb` consuming `LauncherStartupVisualService`
- launcher startup shell composition now routes through `Plain Craft Launcher 2/Application.xaml.vb` consuming `LauncherStartupWorkflowService`
- launcher startup open-count milestone application now routes through `Plain Craft Launcher 2/FormMain.xaml.vb` consuming `LauncherStartupMilestoneService`
- launcher main-window startup composition now routes through `Plain Craft Launcher 2/FormMain.xaml.vb` consuming `LauncherMainWindowStartupWorkflowService`
- launcher startup version-transition setting/file/log application now routes through `Plain Craft Launcher 2/FormMain.xaml.vb` consuming `LauncherVersionTransitionWorkflowService`
- launcher startup update-log rendering now routes through `Plain Craft Launcher 2/Modules/Base/ModUpdateLogShell.vb`
- launcher startup prompt rendering now routes through `Plain Craft Launcher 2/Modules/Base/ModStartupPromptShell.vb`
- launcher application startup immediate-command / bootstrap / visual shell application now routes through `Plain Craft Launcher 2/Modules/Base/ModApplicationStartupShell.vb`
- launcher main-window startup milestone / version-transition shell application now routes through `Plain Craft Launcher 2/Modules/Base/ModMainWindowStartupShell.vb`
- launcher launch prompt / account decision / Java prompt rendering now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchPromptShell.vb`
- launcher Java download confirmation / post-download failure hint shell handling now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModJavaPromptShell.vb`
- launcher crash-result prompt rendering now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModCrashPromptShell.vb`
- launcher crash export picker / completion shell flow now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModCrashExportShell.vb`
- launcher crash-export archive creation now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModCrash.vb` consuming `MinecraftCrashExportArchiveService`
- launcher crash prompt action handling and export completion hint/reveal flow now route through `Plain Craft Launcher 2/Modules/Minecraft/ModCrash.vb` consuming `MinecraftCrashResponseWorkflowService`
- launcher crash-export save dialog defaults now route through `Plain Craft Launcher 2/Modules/Minecraft/ModCrash.vb` consuming `MinecraftCrashResponseWorkflowService`
- launcher in-game music / video / visibility shell application now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchSessionShell.vb`
- launcher prerun options-file mutation now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchOptionsFileService`
- launcher prerun GPU recovery, `launcher_profiles.json`, and `options.txt` composition now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchPrerunWorkflowService`
- launcher prerun `launcher_profiles.json` mutation now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchLauncherProfilesService`
- launcher Minecraft-folder `launcher_profiles.json` creation now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModMinecraft.vb` consuming `MinecraftLauncherProfilesFileService`
- launcher prerun `launcher_profiles.json` retry / reset application now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchLauncherProfilesWorkflowService`
- launcher custom-command / batch-script shell execution now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchCustomCommandService`
- launcher startup session summary logging now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchSessionLogService`
- launcher process start / watcher preparation now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchRuntimeService`
- launcher custom-command and game-process `ProcessStartInfo` construction now route through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchProcessExecutionService`
- launcher custom-command / game-process shell execution now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchExecutionWorkflowService`
- launcher watcher startup composition now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchWatcherWorkflowService`
- launcher launch-session batch export, custom-command shell execution, process shell startup, watcher startup, and post-launch shell composition now route through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchSessionWorkflowService`
- launcher launch script-export completion handling now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchShellService`
- launcher Java requirement / missing-Java recovery orchestration now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchJavaWorkflowService`
- launcher Authlib validate / refresh / authenticate request shaping now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchAuthlibProtocolService`
- launcher Authlib refresh-session resolution and authenticate profile-selection / mutation planning now route through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchAuthlibLoginWorkflowService`
- launcher Microsoft OAuth / XBL / XSTS / profile protocol shaping now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchMicrosoftProtocolService`
- launcher Authlib validate / refresh / authenticate / metadata request coordination now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchAuthlibRequestWorkflowService`
- launcher Microsoft device-code / OAuth refresh / XBL / XSTS / access-token / ownership / profile request coordination now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchMicrosoftRequestWorkflowService`
- launcher Microsoft refresh / XSTS / ownership / profile failure handling now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchMicrosoftFailureWorkflowService`
- launcher launch argument window-size planning now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchResolutionService`
- launcher launch argument final composition now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchArgumentWorkflowService`
- launcher launch classpath ordering now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchClasspathService`
- launcher launch placeholder/replacement value assembly now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchReplacementValueService`
- launcher natives-directory selection now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchNativesDirectoryService`
- launcher natives archive sync now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchNativesSyncService`
- launcher RetroWrapper selection now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchRetroWrapperService`
- launcher JSON argument-section extraction now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchJsonArgumentService`
- launcher JVM argument assembly now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchJvmArgumentService`
- launcher game argument assembly now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchGameArgumentService`
- Authlib role-selection UI is now isolated behind the launch shell adapter instead of being embedded in `ModLaunch.vb`
- Microsoft device-code login popup lifecycle and third-party login failure dialogs are now isolated behind the launch shell adapter instead of being embedded in `ModLaunch.vb`

## Big Goal

Target architecture:

- `PCL.Core.Foundation`
  - pure `net8.0`
  - reusable on macOS/Linux/Windows
  - no WPF
  - no Win32/registry/PInvoke/WMI coupling
- `PCL.Core`
  - Windows-specific adapter/runtime layer
  - current lifecycle/UI integration
  - compatibility facade for existing launcher code

The mission is still “stabilize the runtime boundary before touching the frontend.”

## Current State

Portable runtime/core extraction is now complete for the current review scope, but the project is **not** a finished replacement frontend/backend shell yet.

Handoff decision:

- this is **not** yet a fully working portable backend end-to-end
- this **is** ready to hand to another engineer right now
- the next engineer should treat the backend/runtime extraction as stable, and focus on frontend-shell migration plus the remaining Windows-adapter cleanup

What is already true:

- `PCL.Core.Foundation` is real, headless, and validated on macOS
- `PCL.Core.Backend` now proves a substantial extracted workflow layer can compile as plain `net8.0` without WPF
- `PCL.Core.Backend.Test` now proves that extracted backend slice can execute on macOS in automated tests
- `PCL.Core` now consumes `PCL.Core.Backend` as the canonical implementation of the extracted startup / launch / crash workflow slice
- the major runtime seams are no longer speculative; they exist in code and have tests/build validation
- a large amount of launcher workflow policy has been moved out of WPF/VB files into `PCL.Core`
- the remaining launcher logic is now primarily shell adapters, prompt adapters, concrete network IO, and Windows-facing compatibility code rather than portable runtime policy
- Microsoft device-code popup text / URL / polling contract now comes from a reusable core prompt plan instead of raw launcher-side JSON parsing
- Java runtime install path selection, ignored-file policy, and transfer completion / abort cleanup transitions now come from reusable core services instead of launcher-local rules
- `PCL.Frontend.Spike` is now strong enough to review login request execution, Java runtime download planning, host-backed path wiring, and crash-export target handoff without pulling WPF back into scope
- `PCL.Frontend.Spike` can now also inspect launcher-style Java index / manifest request artifacts and materialize a stub runtime tree from the portable download workflow during `execute` mode
- `PCL.Frontend.Spike` can now also review which Java runtime files are reused versus newly transferred, making partial-runtime recovery visible without WPF
- `PCL.Frontend.Spike` can now also review the post-transfer Java session cleanup / refresh transition for finished vs failed vs aborted runtime downloads
- `PCL.Frontend.Spike` now sources modeled Authlib / Microsoft request URLs, headers, bodies, and Authlib profile-selection / mutation planning from the same core workflow services as the real launcher

What is not true yet:

- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` still owns live request execution, device-code popup polling lifecycle bridging, account/prompt application, and other launcher-side effects around Authlib / Microsoft login flows
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` still owns login step orchestration, device-code popup lifecycle bridging, account/prompt application, and other launcher-side effects around Authlib / Microsoft login flows, but raw live request execution is now isolated behind dedicated launcher request shells
- `Plain Craft Launcher 2/Modules/Minecraft/ModJava.vb` still owns the concrete Java transfer lifecycle, including hashing, loader polling, cancellation, and retry, but launcher-side prompt and cleanup / refresh shell application are now isolated behind dedicated Java shell modules
- startup sequencing is still partly assembled in `Plain Craft Launcher 2/Application.xaml.vb` and `Plain Craft Launcher 2/FormMain.xaml.vb`, but immediate-command / bootstrap / visual shell application and milestone / version-transition application are now isolated behind dedicated startup shell modules
- crash export still has launcher-owned picker / destination / Explorer flow, now isolated in `Plain Craft Launcher 2/Modules/Minecraft/ModCrashExportShell.vb`
- launcher modules now consume `PCL.Core.App.Secrets` and `PCL.Core.App.LauncherIdentity` instead of reading launcher-facing secret and identify values from `PCL.Core.Utils.Secret` directly
- `PCL.Core` still contains deliberate Windows adapter code that is acceptable for now, but not yet wrapped behind the final frontend-facing contracts
- `Utils.Secret` is still deliberately deferred and still blocks a truly headless secure auth/config story

This branch is handoff-ready for another engineer. The next engineer should continue from the existing seams rather than reopening Foundation extraction.

## New Engineer Quick Start

Use this checklist before making changes:

1. read `PORTABILITY_HANDOFF.md` and `FRONTEND_MIGRATION_PLAN.md`
2. inspect the latest extraction commits listed near the end of this document
3. build the portable backend lane first:
   - `dotnet build PCL.Core.Backend/PCL.Core.Backend.csproj -c Debug`
   - `dotnet test PCL.Core.Backend.Test/PCL.Core.Backend.Test.csproj -c Debug`
   - `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- startup`
   - `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- launch legacy-forge`
   - `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- crash`
   - `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- all legacy-forge --mode run --format text`
   - `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- all legacy-forge --mode execute --format text`
4. only then validate the Windows-facing compatibility projects:
   - `dotnet build PCL.Core/PCL.Core.csproj -c Debug`
   - `dotnet build "Plain Craft Launcher 2/Plain Craft Launcher 2.vbproj" -c Debug`

If you need one sentence of orientation: `PCL.Core.Backend` is now the portable source of truth for runtime/workflow policy, while `PCL.Core` and `Plain Craft Launcher 2` should keep shrinking toward adapter-only responsibilities.

## Canonical Boundaries

Treat these statements as current architecture truth:

- `PCL.Core.Foundation` is the lowest-level portable library for headless utilities, storage/network/runtime helpers, and reusable domain primitives
- `PCL.Core.Backend` is the portable backend/workflow assembly and should absorb additional reusable startup / launch / crash runtime services over time
- `PCL.Core` is now a Windows compatibility/adaptation layer and should prefer referencing `PCL.Core.Backend` over carrying duplicate workflow implementations
- `Plain Craft Launcher 2` should keep moving toward prompt rendering, WPF presentation, and Windows shell/interop adapters only
- `PCL.Frontend.Spike` is the safe place to prove a new shell can consume the extracted contracts without pulling WPF back into the backend; use its `run` text transcripts and `execute` workspaces to review adapter boundaries quickly

## What Has Been Completed

### Wave 1

These were already done before the latest continuation:

- created `PCL.Core.Foundation`
- created `PCL.Core.Foundation.Test`
- wired both into `Plain Craft Launcher 2.slnx`
- kept `PCL.Core` referencing Foundation
- moved the first headless chunk into Foundation:
  - logging primitives and file logger
  - non-WPF utility/helpers
  - `Utils.Codecs`
  - `Utils.Diff`
  - `Utils.Encryption`
  - `Utils.Hash`
  - `Utils.Threading`
  - `Utils.Validate`
  - `Utils.VersionControl`
  - `IO.Download`
  - `IO.Storage/HashStorage`
  - `Link/McPing`
  - `Minecraft/ResourceProject`

### Wave 2 Batch 1

These were completed in this branch after the last handoff:

- `Paths` / app environment seam extracted
  - portable app environment and path layout now live in Foundation
  - `PCL.Core.App.Paths` remains API-compatible and delegates into Foundation
- headless config storage extracted
  - config storage/file-provider/migration helpers moved into Foundation
  - Windows-specific shutdown/UI behavior is now reattached through runtime hooks in `PCL.Core`
  - `ConfigService` itself still stays in `PCL.Core`
- proxy/network seam extracted
  - portable proxy core now lives in Foundation
  - Windows registry-backed system proxy monitoring remains in `PCL.Core`
  - `HttpProxyManager.Instance` surface was preserved for existing callers

### Wave 2 Java Slice

This was also completed in this branch:

- Java core types moved into Foundation
  - `JavaManager`
  - `JavaInstallation`
  - `JavaEntry`
  - `JavaStorageItem`
  - Java enums/constants
  - Java scanner/parser interfaces
- portable Java runtime abstractions added in Foundation
  - runtime environment abstraction
  - command runner abstraction
  - storage abstraction for persisted Java entries
  - installation evaluator abstraction for default enablement logic
- portable Java discovery/parsing added in Foundation
  - PATH scanning no longer assumes Windows path separators
  - command lookup scanner can use `where` or `which`
  - command-based Java metadata parser added via `java -XshowSettings:properties -version`
- `PCL.Core.JavaService` now composes:
  - portable command parser
  - Windows PE parser fallback
  - Windows registry scanner
  - Windows Microsoft Store scanner
  - config-backed Java storage adapter in `PCL.Core`

### Wave 3 Launcher Cleanup

These were completed after Wave 2 was declared stable:

- launcher GPU preference handling unified through `PCL.Core.Utils.OS.ProcessInterop`
  - launcher-local registry-writing GPU helper was removed
  - admin-elevation retry behavior for GPU preference remains intact
- launcher-consumable system environment facade added in `PCL.Core`
  - `SystemEnvironmentInfo` now exposes OS summary, physical memory, CPU name, and GPU list
  - Windows WMI-backed collection remains in `PCL.Core`; portable fallback returns safe partial data
- crash-report environment summary moved out of the launcher
  - `MinecraftCrashReportBuilder` now assembles the exported `环境与启动信息.txt` content in `PCL.Core`
  - launcher-side cached CPU/GPU/system-summary globals were removed
- startup environment warning rules moved out of the launcher
  - `LauncherStartupEnvironmentWarningService` now evaluates startup warnings in `PCL.Core`
  - `Application.xaml.vb` only decides how to display the warnings
- launch precheck workflow moved out of the launcher
  - `MinecraftLaunchPrecheckService` now owns launch precheck policy and prompt definitions
  - `ModLaunch.vb` only renders those prompts and applies shell actions
- startup consent workflow moved out of the launcher
  - `LauncherStartupConsentService` now owns special-build / EULA / telemetry prompt sequencing
  - `FormMain.xaml.vb` only renders those prompts and applies shell actions
- startup version upgrade / downgrade policy moved out of the launcher
  - `LauncherVersionTransitionService` now owns version-transition migrations, notices, custom-skin migration selection, and update-log eligibility
  - `FormMain.xaml.vb` applies the returned plan instead of owning that decision tree
- startup version-transition application planning moved out of the launcher
  - `LauncherVersionTransitionWorkflowService` now owns the concrete setup writes, custom-skin copy plan, and related startup log-message planning needed to apply version transitions
  - `FormMain.xaml.vb` now mainly applies the returned shell actions like prompt display, file copy, announcement display, and old-profile migration kickoff
- startup version-isolation migration policy moved out of the launcher
  - `LauncherStartupVersionIsolationMigrationService` now owns legacy `LaunchArgumentIndie` to `LaunchArgumentIndieV2` migration rules
  - `FormMain.xaml.vb` only applies the returned value and log message
- crash export packaging moved out of the launcher
  - `MinecraftCrashExportService` now owns report packaging, filename normalization, and sanitization
  - `ModCrash.vb` keeps picker / zip destination / Explorer opening only
- crash export request assembly moved out of the launcher
  - `MinecraftCrashExportWorkflowService` now owns archive-name suggestion and export-request composition
  - `ModCrash.vb` now only picks the destination and executes the package/export shell actions
- crash result prompt policy moved out of the launcher
  - `MinecraftCrashWorkflowService` now owns crash-result dialog titles, button policy, and export filename suggestion
  - `ModCrash.vb` now only renders the prompt and executes the selected shell action
- post-launch shell policy moved out of the launcher
  - `MinecraftLaunchShellService` now owns completion notification policy, failure titles, and launcher-visibility decisions
  - `ModLaunch.vb` performs the returned shell action instead of deciding it
- launch support prompt policy moved out of the launcher
  - `MinecraftLaunchShellService` now owns launch-count milestone support-prompt wording and button policy
  - `ModLaunch.vb` only renders the returned prompt
- launch GPU-preference failure / retry policy moved out of the launcher
  - `MinecraftLaunchGpuPreferenceWorkflowService` now owns “log directly vs retry as admin” recovery policy plus retry arguments / hint wording
  - `ModLaunch.vb` only executes the returned recovery plan
- launch prerun options-file mutation policy moved out of the launcher
  - `MinecraftLaunchOptionsFileService` now owns `options.txt` target selection, Yosbr fallback behavior, language-format rules, force-unicode initialization, and fullscreen write policy
  - `ModLaunch.vb` now only applies the returned option writes and log messages
- launch prerun composition moved out of the launcher
  - `MinecraftLaunchPrerunWorkflowService` now owns composition of GPU failure recovery planning, Microsoft `launcher_profiles.json` prerun workflow selection, and `options.txt` target-file selection
  - `ModLaunch.vb` now mainly applies the returned prerun shell/file plans instead of rebuilding that launch-prep composition inline
- launch prerun `launcher_profiles.json` mutation policy moved out of the launcher
  - `MinecraftLaunchLauncherProfilesService` now owns Microsoft `launcher_profiles.json` merge/update composition
  - `ModLaunch.vb` now only ensures the file exists, writes the returned JSON, and keeps the retry-after-delete shell flow
- launch custom-command / batch-script planning moved out of the launcher
  - `MinecraftLaunchCustomCommandService` now owns batch-script content generation, UTF-8 vs legacy encoding selection, and custom-command execution plans
  - `ModLaunch.vb` now only writes the returned script and executes the returned command plans
- launch-session startup summary logging moved out of the launcher
  - `MinecraftLaunchSessionLogService` now owns launch-session summary line formatting for launcher / instance / profile diagnostics
  - `ModLaunch.vb` now only prints the returned log lines before watcher startup
- launch process / watcher runtime planning moved out of the launcher
  - `MinecraftLaunchRuntimeService` now owns Java executable selection, process environment composition, process-priority mapping, watcher title fallback rules, and JStack path selection
  - `ModLaunch.vb` now only applies the returned process plan and constructs the watcher with the returned runtime plan
- launch-session start/post-launch composition moved out of the launcher
  - `MinecraftLaunchSessionWorkflowService` now owns composition of batch export content, custom-command shell plans, process shell startup, watcher startup inputs, and post-launch shell policy reuse
  - `ModLaunch.vb` now mainly applies the returned shell plans and watcher adapter inputs instead of rebuilding that session flow step-by-step
- Authlib request / response protocol shaping moved out of the launcher
  - `MinecraftLaunchAuthlibProtocolService` now owns Authlib validate / refresh / authenticate request payloads, response parsing, available-profile extraction, and server-name metadata parsing
  - `ModLaunch.vb` now only performs the HTTP requests, applies prompt/shell actions, and feeds the parsed data into existing workflow services
- Microsoft request / response protocol shaping moved out of the launcher
  - `MinecraftLaunchMicrosoftProtocolService` now owns OAuth refresh parsing, Xbox Live / XSTS request payloads, Minecraft access-token request payloads, ownership parsing, and profile parsing
  - `ModLaunch.vb` now only performs the HTTP requests, applies prompt/shell actions, and feeds the parsed data into the existing Microsoft login workflow
- startup bootstrap policy moved out of the launcher
  - `LauncherStartupBootstrapService` now owns startup directory targets, config preload keys, old log cleanup targets, default update-channel selection, and environment warning message assembly
  - `Application.xaml.vb` consumes the bootstrap result
- startup command parsing and preparation composition moved out of the launcher
  - `LauncherStartupCommandService` now owns recognition of launcher startup commands like `--gpu` and `--memory`
  - `LauncherStartupPreparationService` now composes environment warning collection with bootstrap-policy generation
  - `Application.xaml.vb` now only executes the selected startup shell task and applies the returned preparation result
- startup shell contract added for immediate commands and warning prompts
  - `LauncherStartupShellService` now owns startup immediate-command resolution and environment-warning prompt construction
  - `Application.xaml.vb` consumes the returned plan instead of directly assembling those shell decisions
- startup visual defaults moved out of the launcher
  - `LauncherStartupVisualService` now owns startup splash-screen eligibility plus default tooltip presentation values
  - `Application.xaml.vb` applies the returned visual plan instead of hardcoding those shell defaults inline
- startup open-count milestone policy moved out of the launcher
  - `LauncherStartupMilestoneService` now owns startup-count advancement plus the hidden-theme milestone notice contract
  - `FormMain.xaml.vb` now only applies the returned count update and launcher-local theme-unlock side effect
- startup update-log prompt policy moved out of the launcher
  - `LauncherUpdateLogService` now owns update-log fallback content, title text, button labels, and release-log URL contract
  - `FormMain.xaml.vb` now only loads the changelog file and delegates markdown rendering/browser opening to `ModUpdateLogShell.vb`
- fatal log dialog presentation moved behind a runtime hook
  - `LogRuntimeHooks` now owns fatal-dialog presentation indirection for `PCL.Core.Logging.LogService`
  - the current WPF/VB launcher reattaches the Windows `MsgBox` behavior from `Program.vb`
  - this removes one more direct WPF dialog dependency from shared core logging flow
- launch account prompt policy moved out of the launcher
  - `MinecraftLaunchAccountWorkflowService` now owns Microsoft account-recovery prompts, ownership/profile-required prompts, and Authlib role-selection decisions
  - `ModLaunch.vb` only renders those prompts and applies the returned actions
- launch Java requirement and missing-Java prompt policy moved out of the launcher
  - `MinecraftLaunchJavaRequirementService` now owns launch-time Java version bounds and Mojang component preference calculation
  - `MinecraftLaunchJavaPromptService` now owns missing-Java/manual-download vs auto-download prompt policy
  - `ModLaunch.vb` still performs actual Java selection and Java download execution
- third-party login failure policy moved out of the launcher
  - `MinecraftLaunchThirdPartyLoginWorkflowService` now owns Authlib validation / refresh / login failure wording and wrapped error messages
  - `MinecraftLaunchThirdPartyLoginWorkflowService` now also owns the validate/refresh/login failure transition resolution for advance-vs-abort behavior
  - `ModLaunch.vb` only displays the returned failure dialogs, advances to the returned next step, and throws the returned wrapped errors
- login profile mutation and Microsoft cached-session reuse policy moved out of the launcher
  - `MinecraftLaunchLoginProfileWorkflowService` now owns Microsoft cached-login reuse rules plus Microsoft/Authlib profile creation and update plans
  - `ModLaunch.vb` still performs the network requests and applies the returned mutation plans to launcher profile state
- launch login execution sequencing moved out of the launcher
  - `MinecraftLaunchThirdPartyLoginExecutionService` now owns Authlib validate / refresh / authenticate step sequencing and retry transitions
  - `MinecraftLaunchMicrosoftLoginExecutionService` now owns Microsoft cached-session reuse vs refresh vs device-code flow sequencing plus step progression
  - `ModLaunch.vb` now acts mainly as a step runner that performs requests, prompts, and returned mutation/application work
- launcher login step adapters were further normalized
  - Microsoft login step adapters no longer communicate through launcher-local magic strings like `Ignore` / `Relogin`
  - Authlib validate / refresh / authenticate helpers now return explicit results instead of mutating loader output by side effect
- launcher prompt and shell rendering was split into dedicated shell adapters
  - startup prompt rendering and action application now live in `ModStartupPromptShell.vb`
  - launch prompt rendering, account-decision dialogs, Java prompts, Authlib role selection, Microsoft device-code popup handling, and third-party login failure dialogs now live in `ModLaunchPromptShell.vb`
  - crash-result prompt rendering now lives in `ModCrashPromptShell.vb`
  - this further reduces the amount of mixed UI logic still embedded directly in `Application.xaml.vb`, `FormMain.xaml.vb`, `ModLaunch.vb`, and `ModCrash.vb`
- launch session shell application was split into a dedicated shell adapter
  - `MinecraftLaunchShellService` now owns launch-start and watcher-stop music/video/visibility policy plus launch-count increment contracts
  - `ModLaunchSessionShell.vb` now applies those returned shell actions for both `ModLaunch.vb` and `ModWatcher.vb`
  - this removes another shared decision cluster from launcher-local gameplay lifecycle code
- frontend migration planning artifact added
  - see `FRONTEND_MIGRATION_PLAN.md`

## Important Compatibility Seams

These seams are deliberate and should stay intact until the broader runtime extraction is finished:

- logging seam
  - `PCL.Core.Foundation/Logging/LogWrapper.cs` exposes `AttachLogger(Logger logger)`
  - `PCL.Core/Logging/LogService.cs` owns runtime logger creation
- config storage runtime hooks
  - Foundation storage is now headless
  - `PCL.Core/App/Configuration/Storage/ConfigStorageRuntimeHooks.cs` reattaches Windows-specific shutdown/UI behavior
- proxy seam
  - Foundation owns proxy state/mode selection
  - `PCL.Core/IO/Net/Http/WindowsRegistrySystemProxySource.cs` owns the Windows registry-backed system proxy adapter
- Java seam
  - Foundation owns Java runtime/discovery core
  - `PCL.Core/Minecraft/Java/JavaService.cs` still owns the Windows assembly of scanners/parsers/storage

## Recommended Next Work

Before a real frontend migration starts, the remaining work is now mostly adapter cleanup rather than new backend architecture. The next engineer should treat the current backend seams as stable and finish the following tracks first.

## Remaining Before Frontend Migration

These are the remaining parts to finish before calling the project ready for a real frontend migration.

1. Finish `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` as a thin login/launch shell adapter.
   - Keep `PCL.Core.Backend` as the source of truth for request planning, failure transitions, launch composition, and process start requests.
   - Pull any remaining reusable request execution / coordination out of `ModLaunch.vb` where it is still mixed with launcher-local state transitions.
   - Reduce the remaining inline launcher notifications, device-code popup lifecycle glue, account decision handling, and post-step shell side effects to adapter-only behavior.
2. Finish `Plain Craft Launcher 2/Modules/Minecraft/ModJava.vb` as a thin Java transfer adapter.
   - The Java runtime transfer plan and reused-file selection are already portable.
   - The remaining work is the concrete download engine lifecycle: file hashing, loader polling, cancellation, retry, cleanup, and Java list/runtime refresh after install.
   - A frontend migration should not start while those behaviors are still effectively launcher-owned.
3. Finish startup adapter cleanup in `Plain Craft Launcher 2/Application.xaml.vb` and `Plain Craft Launcher 2/FormMain.xaml.vb`.
   - Leave splash screen, window lifetime, tooltip metadata, and WPF presentation in the launcher.
   - Keep moving any startup sequencing, prompt timing, and decision flow that is still reusable out of those files and into backend-facing contracts.
4. Finish crash shell cleanup in `Plain Craft Launcher 2/Modules/Minecraft/ModCrash.vb`.
   - The backend already owns crash export planning, naming, save-dialog defaults, and completion policy.
   - The remaining launcher-owned work should shrink toward picker invocation, chosen-destination plumbing, direct log opening, and Explorer reveal only.
5. Reduce the Windows-only helper surface that still leaks into reusable backend paths.
   - Highest-value examples remain `PCL.Core/IO/Files.cs`, `PCL.Core/App/Tools/DependencyCheckService.cs`, and dialog / clipboard / shell helpers that still combine reusable logic with Windows-only APIs.
   - The goal is not to make those helpers portable in place; it is to wrap or split them so a future frontend does not need them as backend dependencies.
6. Resolve `Utils.Secret` enough for a headless auth/config story.
   - This is still the biggest architectural holdout for a truly portable end-to-end backend.
   - The next engineer does not need to redesign all secrets/auth storage at once, but they do need a boundary that a non-Windows frontend can eventually call.
7. Keep extending `PCL.Frontend.Spike` only where it proves the remaining seams.
   - The spike is already good enough for startup / launch / crash contract review, login request execution transcripts, Java request planning, Java transfer/reuse review, crash export destination handoff, launch-script export, host-backed inputs, and portable process-start request inspection.
   - The next spike work should focus on proving the remaining adapter seams above, not rebuilding frontend concerns.

## Recommended Next Work

Highest-value next slices, in order:

1. finish shrinking `ModLaunch.vb`
   - focus on live Authlib / Microsoft request execution adapters, remaining popup/prompt bridges, and any launcher-only notifications still mixed with reusable login or launch steps
2. finish shrinking `ModJava.vb`
   - focus on the real transfer lifecycle, cancellation/retry, cleanup, and runtime refresh path
3. reduce startup orchestration further
   - continue trimming `Application.xaml.vb` and `FormMain.xaml.vb` so they become shell/application adapters rather than owners of startup decision flow
4. trim `ModCrash.vb`
   - keep only picker / destination / Explorer shell work in the launcher
5. move more reusable runtime/services from `PCL.Core` into `PCL.Core.Backend`
   - prefer migrating portable service implementations instead of adding new workflow code to the Windows-targeted project
   - keep `PCL.Core` focused on adapters, compatibility surfaces, and Windows-only composition
6. keep extending the shell-replacement spike
   - the next extension should prove the remaining adapter seams above, especially login execution and Java transfer lifecycle boundaries

Work that should stay in launcher adapters for now:

- message boxes / markdown dialogs / hints
- WPF dispatcher marshaling
- window activation / visibility / Explorer / browser opening
- raw Win32 interop and monitor/window manipulation

Good handoff starting files:

- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb`
- `Plain Craft Launcher 2/Application.xaml.vb`
- `Plain Craft Launcher 2/FormMain.xaml.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModCrash.vb`
- `FRONTEND_MIGRATION_PLAN.md`

## Current Verified Status

### SDK / build environment

Verified on this machine:

- `.NET SDK 10.0.201`
- `.NET SDK 8.0.419`
- `global.json` pins `10.0.201`

### Current validation state

Verified successfully after the latest changes:

- `dotnet build PCL.Core.Foundation/PCL.Core.Foundation.csproj -c Debug`
- `dotnet test PCL.Core.Foundation.Test/PCL.Core.Foundation.Test.csproj -c Debug`
- `dotnet build PCL.Core/PCL.Core.csproj -c Debug`
- `dotnet build PCL.Core.Test/PCL.Core.Test.csproj -c Debug`
- `dotnet build PCL.Core.Backend/PCL.Core.Backend.csproj -c Debug`
- `dotnet test PCL.Core.Backend.Test/PCL.Core.Backend.Test.csproj -c Debug`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- startup`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- launch legacy-forge`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- crash`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- all legacy-forge --mode run --format text`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- all legacy-forge --mode execute --format text`
- `dotnet build "Plain Craft Launcher 2/Plain Craft Launcher 2.vbproj" -c Debug`

Current Foundation test count:

- `99/99` passing

Current Foundation regression scan is clean:

- no `System.Windows`
- no `Microsoft.Win32`
- no `DllImport`
- no `LibraryImport`

Additional note about validation on this machine:

- `PCL.Core.Test` builds successfully on macOS, but running that `net8.0-windows` test assembly is blocked locally because `Microsoft.WindowsDesktop.App` testhost runtime is not available on this host
- `PCL.Core.Backend.Test` is the new portable test lane for extracted backend workflows and does run successfully on this macOS host

## Current Portability Estimate

Use these numbers when handing the work to another engineer:

- portable workflow/policy backend coverage: about `98%`
- backend readiness for a replacement frontend shell: about `95%~96%`
- truly portable end-to-end backend: about `90%~91%`

Why those numbers are different:

- `PCL.Core.Foundation` is already real, portable, and validated on macOS
- `PCL.Core.Backend` now owns most of the launcher workflow/policy seams that matter for startup / launch / crash
- the remaining work is no longer “prove the architecture”; it is finishing the last launcher-owned live adapters and Windows-bound helper seams so a replacement frontend can stand on top of them cleanly

Concrete current state:

- `PCL.Core.Foundation` is the strongest part of the portability work
- `PCL.Core.Backend` is already a credible portable source of truth for the extracted workflow layer
- `PCL.Core` still contains a smaller but meaningful mix of Windows-only adapters and compatibility helpers
- `Plain Craft Launcher 2` still owns the last major adapter-heavy launch / Java / startup / crash behaviors that must be trimmed before a real frontend migration
- the frontend is still the biggest blocker to a cross-platform launcher binary, but the backend is now close enough that the remaining work should be framed as adapter cleanup rather than new backend architecture

## Recent Checkpoint Commits

This is the meaningful history for the current portability work:

- `3771109e` `Extract portable launch process execution`
- `e07aad7b` `Model launch script export in shell services`
- `79b8db58` `Extract crash export dialog planning`
- `64703c2a` `Refresh migration handoff for Java transfer seam`
- `6b332959` `Model Java transfer reuse in shell spike`
- `094e6d67` `Extract Java download transfer planning`
- `6108afda` `Refresh migration handoff for next owner`
- `1c2847e7` `Extract third-party login failure transitions`
- `894fe9ca` `Clean up launch adapter helpers and docs`
- `8899b936` `Extract Authlib login response workflow into core`
- `e44bc97b` `Extract Microsoft login failure policy into core`
- `f75a3a58` `Extract login request planning into core workflows`
- `ffccf8fb` `Trim crash response shell orchestration`
- `b899edbc` `Trim launch Java selection orchestration`
- `42c87fa5` `Refresh handoff for Java workflow progress`

- `3bf8d465` `Document shell spike input replay workflow`
- `0693fbac` `Add file-backed input replay to shell spike`
- `753a11ea` `Fix auto-generated shell spike workspace ids`
- `d57b87b0` `Document shell spike execution mode`
- `3b18efa5` `Add executable workspace mode to shell spike`
- `461a8a92` `Document replacement shell spike workflow`
- `b5c40f8e` `Expand spike into replacement shell prototype`
- `2e1225de` `Mark portable runtime extraction complete for review`
- `4e1c155c` `Route launcher argument assembly through backend services`
- `7ff46b57` `Extract portable launch argument assembly services`
- `9d374376` `Update portability estimate after natives extraction`
- `9dbe1d5f` `Route launcher natives handling through backend services`
- `f9cf278e` `Make windows core consume portable backend`
- `5f509f86` `Create portable backend assembly and test lane`
- `24b3cfcf` `Extract startup version transition application plan`
- `4ac73df0` `Extract launch prerun workflow composition`
- `12f77990` `Refactor launcher to consume session workflow plan`
- `c3d402b3` `Add launch session workflow planning seam`
- `3485cf8b` `Extract startup workflow composition`
- `d08c1b2f` `Extract launch execution workflow`
- `1b73dd43` `Extract crash export archive service`
- `46c7567c` `Extract launch watcher workflow`
- `b0c55d2d` `Extract launcher_profiles workflow and seed`
- `f92822b7` `Update migration handoff for shell spike`
- `ab55901f` `refactor(paths): extract portable app path layout into foundation`
- `ad18ee67` `refactor(config): move headless config storage into foundation`
- `633c41a8` `refactor(network): split proxy core from windows registry adapter`
- `37aa3d5e` `test(portability): add regression coverage and rerun mac build matrix`
- `91428de6` `refactor(java): extract portable java runtime and scanning core`
- `9422e395` `test(java): add portable runtime and scanner coverage`
- `4209a9bd` `refactor(launcher): route gpu preference through process interop`
- `d4b8d415` `feat(runtime): add launcher system environment facade`
- `9ce41769` `refactor(launcher): source system summary from core facade`
- `24a3332a` `docs(portability): outline frontend migration tracks`
- `767e6fcf` `refactor(crash): move environment report assembly into core`
- `20dcecfc` `refactor(startup): move environment warning rules into core`
- `89a1474a` `feat(launch): add core launch precheck workflow models`
- `cead4dcf` `refactor(launcher): route launch precheck through core workflow`
- `3da5635e` `feat(startup): add startup consent workflow service`
- `ccfda6a5` `refactor(launcher): route startup prompts through core workflow`
- `4b381762` `feat(crash): add crash export packaging service`
- `3621fa20` `refactor(launcher): route crash export through core helper`
- `ef054bb4` `test(migration): cover launcher workflow extraction regressions`
- `3c448760` `refactor(launch): extract post-launch shell policy`
- `3c9edf1e` `refactor(startup): extract bootstrap policy`
- `510b2974` `feat(launch): add account prompt workflow service`
- `2dc7194d` `refactor(launcher): route launch account prompts through core workflow`
- `ba5c9882` `feat(launch): add java requirement and prompt workflow`
- `ee20387b` `refactor(launcher): route java launch policy through core workflow`
- `8d139e36` `test(migration): cover launch workflow extraction regressions`
- `2ed9ce55` `feat(launch): add third-party login workflow service`
- `e5960538` `refactor(launcher): route authlib failure policy through core workflow`
- `1929b08f` `feat(launch): add login profile workflow service`
- `dc49a7f2` `refactor(launcher): route login profile workflow through core service`
- `f3cb24d6` `feat(startup): add launcher version transition workflow`
- `c0b1ac9f` `refactor(launcher): route version migration through core workflow`
- `9c3ed1fc` `feat(startup): add version isolation migration workflow`
- `c3e9949b` `refactor(startup): route version isolation migration through core workflow`
- `5ae28075` `feat(crash): add export workflow planner`
- `93fdf3f3` `refactor(crash): route export assembly through core workflow`
- `8fdaf8e4` `feat(launch): add shell prompt and gpu retry workflows`
- `3f339b39` `refactor(launcher): route launch shell policies through core workflows`
- `eb72c877` `refactor(launch): route auth refresh profile updates through core workflow`
- `5e1b4ee3` `feat(launch): extract authlib login execution workflow`
- `1d67a31d` `feat(launch): extract microsoft login execution workflow`
- `d881161b` `refactor(launch): replace microsoft login magic step markers`
- `9899021f` `refactor(launch): return authlib login step results explicitly`
- `4b6ab80e` `feat(crash): add core crash workflow service`
- `94ebef70` `refactor(crash): consume core crash workflow prompt service`
- `590ffbd5` `refactor(startup): extract startup command and preparation services`
- `b2332da3` `docs(portability): note startup workflow extraction`
- `4ab0c302` `refactor(logging): move fatal dialog presentation behind runtime hook`
- `7deae4f8` `Add startup shell service contract`
- `67c9c0cd` `Refactor startup shell handling in launcher`
- `28d0726c` `Extract startup prompt shell adapter`
- `f81f56f5` `Extract launch prompt shell adapter`
- `53c09fdf` `Extract crash prompt shell adapter`
- `41f13f26` `Extract auth profile selection shell flow`
- `2ce1d80e` `Extract login dialog shell handling`
- `b2a4e939` `Add spike launch login execution modeling`
- `622b0e36` `Add explicit crash export path handling`
- `4f52b60a` `Add host-backed spike input mode`
- `cbe6a6f3` `Extract Java runtime download planning`

If the next engineer wants to understand the current extraction shape, start with the newest seven commits above, then continue downward through the earlier Wave 3 / Wave 2 history.

## Wave 2 Completion Status

Wave 2 runtime/platform extraction is complete.

The Windows-bound runtime responsibilities that were still mixed into shared services have now been isolated behind explicit Windows adapters or platform services:

- process/platform service abstraction completed
  - non-admin process lifecycle is portable in Foundation
  - Windows admin/UAC/WMI/GPU preference behavior is isolated in `WindowsProcessPlatformService`
- system theme probing completed
  - theme detection is isolated in `WindowsRegistrySystemThemeSource`
- registry monitoring completed
  - registry change observation is isolated in `WindowsRegistryChangeMonitor`
- telemetry OS and registry probes completed
  - official launcher detection is isolated in `WindowsRegistryOfficialLauncherUsageProbe`
  - startup/telemetry runtime inspection flows through `SystemRuntimeInfoSourceProvider`
- directory permission probing completed
  - Windows ACL-based logic is isolated in `WindowsAclDirectoryPermissionService`
  - portable fallback probing exists for non-Windows environments

What remains in `PCL.Core` is now either:

- deliberate Windows adapter/implementation code
- launcher/UI-specific Windows code in WPF/VB layers
- explicitly deferred secure/device identity code under `Utils.Secret`

## Recommended Next Steps

The next engineer should treat the runtime extraction as finished and the launcher-workflow cleanup as well underway, but not fully complete. Do not reopen the runtime seams that are now stable unless a migration blocker forces it.

Recommended order:

1. keep `PCL.Core.Foundation` stable and avoid leaking UI/Win32 concerns back into it
2. continue shrinking `PCL.Core` itself by moving reusable non-UI behavior away from WPF/UI infrastructure and Windows-only dialog/shell code
3. use `FRONTEND_MIGRATION_PLAN.md` as the working migration brief
4. treat launch login-orchestration as largely addressed and begin a small shell-replacement / frontend-contract phase
5. keep `ModLaunch.vb` cleanup incremental: only peel off remaining request-execution adapters, Java download shell bridging, or prompt wrappers when they materially simplify a new shell consumer
6. focus the next migration spike on replacing or paralleling a narrow launcher surface while continuing to reuse `PCL.Core` launch/startup/crash services
7. if additional workflow logic must move before a frontend cutover, keep moving it into `PCL.Core` services instead of duplicating it in the launcher
8. keep validating with `PCL.Core.Backend.Test`, the spike, and launcher builds after each extraction slice
9. do not claim “fully portable backend” yet; there is still meaningful Windows-specific backend code left in `PCL.Core`

Suggested next engineer targets, in priority order:

1. keep trimming `ModLaunch.vb`
   - highest-value slices now are remaining request-execution adapters, Java download job lifecycle / retry shell bridging, and any post-step launcher notifications or dialogs still inline in the step runner
2. keep `Application.xaml.vb` and `FormMain.xaml.vb` as shell adapters only
   - do not move more policy back into them; keep extracting startup composition if a new shell consumer needs it
3. trim `ModCrash.vb` further only where it helps a future shell consumer
   - likely next slices: save-picker invocation, export destination handling, success hint / Explorer opening
4. keep reducing `PCL.Core` WPF coupling
   - likely candidates: dialog/clipboard/system-dialog helpers, UI-facing logging/hint/message-box seams, and any service that currently depends on dispatcher-bound wrappers only for presentation
5. only touch Windows interop helpers when the goal is to isolate them better
   - not to make them “portable” in place
6. begin shrinking `PCL.Core` itself once the launcher shell adapters are no longer the main blocker
   - highest-value examples right now: `PCL.Core/IO/Files.cs`, `PCL.Core/App/Tools/DependencyCheckService.cs`, and other helpers that still combine reusable logic with Windows-only shell APIs
7. avoid spending time on visual/frontend replacement until the next narrow shell contract is chosen

## Important Non-Goals Right Now

Do not do these yet unless the runtime boundary is already stable:

- replace the WPF/VB frontend
- rename public namespaces from `PCL.Core.*` to `PCL.Core.Foundation.*`
- move `ConfigService` wholesale “as-is”
- move `Utils.Secret` wholesale “as-is”
- try to make the whole launcher runnable cross-platform before the runtime seams are done

## Known Risks / Sticky Areas

- `ConfigService` is still in `PCL.Core`; only the storage/runtime seam is headless
- Java launch and remaining launcher-side VB/WPF flows still contain Windows assumptions even though the runtime/discovery core is portable
- `Utils.Secret` still blocks a truly headless secure config/auth story and remains explicitly deferred
- the frontend is still WPF/VB and therefore still the biggest blocker to an actually cross-platform launcher binary
- the largest former workflow blocker, launch login execution / orchestration, is now largely expressed through `PCL.Core` execution and mutation services, but the step adapters in `ModLaunch.vb` are still VB/WPF-coupled
- Java runtime manifest selection and download file planning are now portable, but the actual launcher-managed download job lifecycle and retry UX are still adapter-owned
- `Application.xaml.vb` still owns startup command execution shell work and WPF-specific startup presentation
- `ModCrash.vb` still owns save-picker invocation, export destination flow, and Explorer opening
- `PCL.Core` still contains Windows-only shell/platform helpers such as `IO/Files.cs` shortcut/dialog logic and `App/Tools/DependencyCheckService.cs` Microsoft Store / PowerShell checks
- frontend migration is now mostly blocked by remaining shell adapter cleanup and `PCL.Core` Windows-helper reduction, not by runtime-core portability itself

## Working Rules For The Next Engineer

- preserve compatibility-first behavior
- do not break existing static/public entry points unless there is no alternative
- keep adding small checkpoint commits after each green validation slice
- run `dotnet test PCL.Core.Foundation.Test/PCL.Core.Foundation.Test.csproj -c Debug`
- run `dotnet build PCL.Core/PCL.Core.csproj -c Debug`
- rerun the Foundation Windows-API scan after each extraction chunk

## Recent Completion Commits

Use this shorter checkpoint list for handoff orientation:

- `3771109e` `Extract portable launch process execution`
- `e07aad7b` `Model launch script export in shell services`
- `79b8db58` `Extract crash export dialog planning`
- `64703c2a` `Refresh migration handoff for Java transfer seam`
- `6b332959` `Model Java transfer reuse in shell spike`
- `094e6d67` `Extract Java download transfer planning`
- `6108afda` `Refresh migration handoff for next owner`
- `1c2847e7` `Extract third-party login failure transitions`
- `894fe9ca` `Clean up launch adapter helpers and docs`
- `8899b936` `Extract Authlib login response workflow into core`
- `e44bc97b` `Extract Microsoft login failure policy into core`
- `f75a3a58` `Extract login request planning into core workflows`

The longer Wave 2 / earlier Wave 3 history is preserved in the `Recent Checkpoint Commits` section above.

## Phase Call

Treat the project as ready for a handoff into frontend-migration preparation, but not yet ready for a real frontend cutover.

The recommended near-term phase is:

1. finish the remaining adapter-heavy launcher cleanup, mainly in `ModLaunch.vb`, `ModJava.vb`, `Application.xaml.vb`, `FormMain.xaml.vb`, and `ModCrash.vb`
2. reduce the last Windows-helper dependencies in `PCL.Core` that still block a future frontend consumer
3. only then start a real frontend migration on top of those seams

## One-Line Summary

Wave 2 is complete and the backend extraction is now far enough along to hand off confidently: startup / crash / launch policy is largely portable, Java transfer planning and process start construction are now shared seams, and the next engineer should finish the remaining adapter cleanup plus `PCL.Core` Windows-helper reduction before attempting a real frontend migration.

## Updated Bottom Line

This repository is ready to hand to another engineer.

The correct framing for that handoff is:

- Wave 2 runtime/platform extraction is complete
- Wave 3 launcher-workflow cleanup is well underway and has already removed a large amount of mixed prompt/policy logic from the launcher files
- the project does **not** yet have a fully working cross-platform backend end-to-end, because the last live launcher adapters and some Windows-specific runtime/shell helpers are still in the way
- the next engineer should not restart the portability effort; they should continue the current extraction path by:
  - finishing the remaining launcher shell-adapter cleanup
  - then shrinking `PCL.Core` Windows coupling
  - then starting frontend migration only after those remaining seams are adapter-dominant instead of workflow-dominant
