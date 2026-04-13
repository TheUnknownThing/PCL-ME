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

- a new portable `net8.0` backend assembly now lives in `PCL.Core.Backend`, compiling the extracted startup / launch / crash workflow layer outside `PCL.Core`'s `net8.0-windows` target
- a new portable backend test lane now lives in `PCL.Core.Backend.Test`, and its extracted workflow/service coverage runs on macOS/Linux hosts
- `PCL.Frontend.Spike` now targets plain `net8.0` and runs against `PCL.Core.Backend` instead of `PCL.Core`
- `PCL.Core` now references `PCL.Core.Backend` and no longer compiles that extracted workflow slice directly, making the portable backend assembly the source of truth for those services
- launch Java requirement / missing-Java recovery workflow now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchJavaWorkflowService`
- a thin replacement-shell spike now lives in `PCL.Frontend.Spike` and can exercise extracted startup / launch / crash services without WPF page code
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
- Microsoft request / response protocol shaping now lives in `PCL.Core.Minecraft.Launch.MinecraftLaunchMicrosoftProtocolService`
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
- launcher launch prompt / account decision / Java prompt rendering now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchPromptShell.vb`
- launcher crash-result prompt rendering now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModCrashPromptShell.vb`
- launcher crash-export archive creation now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModCrash.vb` consuming `MinecraftCrashExportArchiveService`
- launcher in-game music / video / visibility shell application now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchSessionShell.vb`
- launcher prerun options-file mutation now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchOptionsFileService`
- launcher prerun GPU recovery, `launcher_profiles.json`, and `options.txt` composition now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchPrerunWorkflowService`
- launcher prerun `launcher_profiles.json` mutation now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchLauncherProfilesService`
- launcher Minecraft-folder `launcher_profiles.json` creation now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModMinecraft.vb` consuming `MinecraftLauncherProfilesFileService`
- launcher prerun `launcher_profiles.json` retry / reset application now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchLauncherProfilesWorkflowService`
- launcher custom-command / batch-script shell execution now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchCustomCommandService`
- launcher startup session summary logging now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchSessionLogService`
- launcher process start / watcher preparation now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchRuntimeService`
- launcher custom-command / game-process shell execution now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchExecutionWorkflowService`
- launcher watcher startup composition now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchWatcherWorkflowService`
- launcher launch-session batch export, custom-command shell execution, process shell startup, watcher startup, and post-launch shell composition now route through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchSessionWorkflowService`
- launcher Java requirement / missing-Java recovery orchestration now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchJavaWorkflowService`
- launcher Authlib validate / refresh / authenticate request shaping now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchAuthlibProtocolService`
- launcher Microsoft OAuth / XBL / XSTS / profile protocol shaping now routes through `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb` consuming `MinecraftLaunchMicrosoftProtocolService`
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

What is already true:

- `PCL.Core.Foundation` is real, headless, and validated on macOS
- `PCL.Core.Backend` now proves a substantial extracted workflow layer can compile as plain `net8.0` without WPF
- `PCL.Core.Backend.Test` now proves that extracted backend slice can execute on macOS in automated tests
- `PCL.Core` now consumes `PCL.Core.Backend` as the canonical implementation of the extracted startup / launch / crash workflow slice
- the major runtime seams are no longer speculative; they exist in code and have tests/build validation
- a large amount of launcher workflow policy has been moved out of WPF/VB files into `PCL.Core`
- the remaining launcher logic is now primarily shell adapters, prompt adapters, concrete network IO, and Windows-facing compatibility code rather than portable runtime policy

What is not true yet:

- `ModLaunch.vb` still owns adapter-level request execution, Java-selection / download shell bridging, and launcher prompt integration
- startup sequencing is still partly assembled in `Application.xaml.vb` and `FormMain.xaml.vb`
- crash export still has launcher-owned picker / zip / Explorer flow in `ModCrash.vb`
- `PCL.Core` still contains deliberate Windows adapter code that is acceptable for now, but not yet wrapped behind the final frontend-facing contracts

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
- `PCL.Frontend.Spike` is the safe place to prove a new shell can consume the extracted contracts without pulling WPF back into the backend

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
  - `ModLaunch.vb` only displays the returned failure dialogs and throws the returned wrapped errors
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

Highest-value next slices, in order:

1. move more reusable runtime/services from `PCL.Core` into `PCL.Core.Backend`
   - prefer migrating portable service implementations instead of adding new workflow code to the Windows-targeted project
   - keep `PCL.Core` focused on adapters, compatibility surfaces, and Windows-only composition
2. reduce `ModLaunch.vb` further
   - finish extracting reusable request execution / coordination and Java-selection or Java-download bridging that is not inherently UI-specific
   - keep shell prompts, popup lifecycle, and launcher-local side effects in adapters
3. reduce startup orchestration further
   - continue trimming `Application.xaml.vb` and `FormMain.xaml.vb` so they become shell/application adapters rather than owners of startup decision flow
4. trim `ModCrash.vb`
   - move any remaining reusable crash-export shell decisions or file-preparation workflow into `PCL.Core.Backend`
5. keep extending the shell-replacement spike
   - prove that a small non-WPF shell can consume the extracted startup / launch / crash contracts without rewriting the whole launcher

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

If “portable backend” means “the non-GUI services a future macOS/Linux launcher could call without depending on WPF or Windows-only APIs”, the current estimate is:

- about `92%~94%` complete

Rationale:

- `PCL.Core.Foundation` is already real, portable, and validated on macOS
- a substantial amount of launcher workflow policy has already been extracted into headless/core services
- the biggest remaining backend gap is no longer basic business logic extraction; it is the separation of reusable services from `PCL.Core`’s remaining WPF/UI and Windows-adapter code
- the residual blockers are now concentrated in a smaller set of launcher shell adapters, especially `ModLaunch.vb` and `Application.xaml.vb`

Concrete current state:

- `PCL.Core.Foundation` is the strongest part of the portability work
- `PCL.Core` still contains a significant mix of:
  - reusable runtime/backend services
  - WPF/UI infrastructure
  - Windows-only adapters and interop helpers
- the launcher now consumes core-owned startup/crash/launch shell policies for more of its migration and prompt logic than before, but some step orchestration is still VB/WPF-local
- the frontend is still the biggest blocker to a cross-platform launcher binary, but the backend is already far enough along that a new frontend can be built on top of the extracted seams

If “fully working portable backend” means “all non-frontend launcher behavior can run cross-platform today without the current Windows launcher layer”, the honest estimate is lower:

- about `74%~78%` complete

Reason:

- `PCL.Core.Foundation` is genuinely portable
- `PCL.Core` still targets `net8.0-windows` and still mixes reusable backend/runtime services with Windows adapters, WPF-facing helpers, and shell/device integrations
- the remaining work is no longer proving the architecture; it is continuing the separation until a new frontend can consume the backend without depending on those Windows-specific layers

## Recent Checkpoint Commits

This is the meaningful history for the current portability work:

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

If the next engineer wants to understand the current extraction shape, start with the twelve newest commits above, then continue downward through the earlier Wave 1 / Wave 2 extraction history.

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
4. treat the launch login-orchestration blocker as mostly addressed and begin a small shell-replacement / frontend-contract phase
5. keep `ModLaunch.vb` cleanup incremental: only peel off remaining step-local adapter glue or prompt/shell wrappers when they materially simplify a new shell consumer
6. focus the next migration spike on replacing or paralleling a narrow launcher surface while continuing to reuse `PCL.Core` launch/startup/crash services
7. if additional workflow logic must move before a frontend cutover, keep moving it into `PCL.Core` services instead of duplicating it in the launcher
8. keep validating with the Foundation test suite and Foundation API scan whenever new shared logic is moved
9. do not claim “fully portable backend” yet; there is still meaningful Windows-specific backend code left in `PCL.Core`

Suggested next engineer targets, in priority order:

1. keep reducing `PCL.Core` WPF coupling
   - likely candidates: dialog/clipboard/system-dialog helpers, UI-facing logging/hint/message-box seams, and any service that currently depends on dispatcher-bound wrappers only for presentation
2. keep `Application.xaml.vb` and `FormMain.xaml.vb` as shell adapters only
   - do not move more policy back into them
3. keep trimming `ModLaunch.vb`
   - likely next slices: remaining launch-step adapter glue, Java-selection / download shell bridging, and post-step launcher notifications or dialogs that still sit inline in the step runner
4. keep trimming `Application.xaml.vb`
   - likely next slices: startup command execution shell flow and remaining startup presentation hooks that still assemble local policy
5. only touch Windows interop helpers when the goal is to isolate them better
   - not to make them “portable” in place
6. trim `ModCrash.vb` further only where it helps a future shell consumer
   - likely next slices: save-picker invocation, export destination handling, success hint / Explorer opening
7. begin shrinking `PCL.Core` itself once the launcher shell adapters are no longer the main blocker
   - highest-value examples right now: `PCL.Core/IO/Files.cs`, `PCL.Core/App/Tools/DependencyCheckService.cs`, and other helpers that still combine reusable logic with Windows-only shell APIs
8. avoid spending time on visual/frontend replacement until the next narrow shell contract is chosen

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

The final Wave 2 checkpoint history after the previous handoff is:

- `5e2cf992` `refactor(process): add portable foundation process manager`
- `a497f672` `refactor(process): delegate core process helpers through windows facade`
- `459b79ce` `test(portability): rerun process portability validation`
- `4873eb24` `refactor(theme): isolate system theme reads behind windows source`
- `64e00ad3` `refactor(telemetry): isolate official launcher probe behind windows adapter`
- `08edf3ef` `test(portability): rerun theme and telemetry validation`
- `5e0e7c92` `refactor(runtime): add system runtime probe adapters`
- `b64cf13f` `refactor(runtime): route startup and telemetry through runtime probe`
- `4e8a7134` `test(portability): rerun runtime probe validation`
- `0728569d` `refactor(registry): isolate registry monitor behind windows implementation`
- `22c972a3` `refactor(proxy): route registry proxy monitoring through adapter`
- `946b1569` `test(portability): rerun registry adapter validation`
- `e88b5d55` `refactor(process): add windows process platform service`
- `d317faf3` `refactor(process): delegate interop facade through platform service`
- `b95cc838` `test(portability): rerun process platform validation`
- `cd8da717` `refactor(io): isolate directory permission checks behind platform service`
- `1bb1befe` `refactor(runtime): route easytier and dependency checks through runtime info`

The Wave 3 launcher cleanup commits after that are:

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
- `7deae4f8` `Add startup shell service contract`
- `67c9c0cd` `Refactor startup shell handling in launcher`
- `28d0726c` `Extract startup prompt shell adapter`
- `f81f56f5` `Extract launch prompt shell adapter`
- `53c09fdf` `Extract crash prompt shell adapter`
- `41f13f26` `Extract auth profile selection shell flow`
- `2ce1d80e` `Extract login dialog shell handling`

## Phase Call

Do not treat the project as ready for a true “next phase” frontend-shell replacement yet.

It is ready for handoff to another engineer, but the recommended near-term phase is still:

1. finish the last high-value launcher workflow extraction slices, now concentrated mainly in `ModLaunch.vb` and `Application.xaml.vb`
2. then start a narrow shell-migration spike

## One-Line Summary

Wave 2 is complete and a substantial Wave 3 cleanup set is landed: the runtime/core portability seams are stabilized, startup/crash/launch shell policies now have broader core-side coverage, Foundation remains headless and macOS-valid, and the next engineer should finish the remaining launcher shell cleanup plus `PCL.Core` Windows-helper reduction before claiming a truly portable backend or attempting a real frontend-shell cutover.

## Updated Bottom Line

This repository is ready to hand to another engineer.

The correct framing for that handoff is:

- Wave 2 runtime/platform extraction is complete
- Wave 3 launcher-workflow cleanup is well underway and has already removed a large amount of mixed prompt/policy logic from the launcher files
- the project does **not** yet have a fully working cross-platform backend end-to-end, because `PCL.Core` still contains Windows-specific runtime and shell helpers
- the next engineer should not restart the portability effort; they should continue the current extraction path by:
  - finishing the remaining launcher shell-adapter cleanup
  - then shrinking `PCL.Core` Windows coupling
  - then choosing a narrow frontend-shell replacement spike on top of those seams
