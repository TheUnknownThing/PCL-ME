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
- crash export packaging moved out of the launcher
  - `MinecraftCrashExportService` now owns report packaging, filename normalization, and sanitization
  - `ModCrash.vb` keeps picker / zip destination / Explorer opening only
- crash result prompt policy moved out of the launcher
  - `MinecraftCrashWorkflowService` now owns crash-result dialog titles, button policy, and export filename suggestion
  - `ModCrash.vb` now only renders the prompt and executes the selected shell action
- post-launch shell policy moved out of the launcher
  - `MinecraftLaunchShellService` now owns completion notification policy, failure titles, and launcher-visibility decisions
  - `ModLaunch.vb` performs the returned shell action instead of deciding it
- startup bootstrap policy moved out of the launcher
  - `LauncherStartupBootstrapService` now owns startup directory targets, config preload keys, old log cleanup targets, default update-channel selection, and environment warning message assembly
  - `Application.xaml.vb` consumes the bootstrap result
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

## Recent Checkpoint Commits

This is the meaningful history for the current portability work:

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
- `eb72c877` `refactor(launch): route auth refresh profile updates through core workflow`
- `5e1b4ee3` `feat(launch): extract authlib login execution workflow`
- `1d67a31d` `feat(launch): extract microsoft login execution workflow`
- `d881161b` `refactor(launch): replace microsoft login magic step markers`
- `9899021f` `refactor(launch): return authlib login step results explicitly`
- `4b6ab80e` `feat(crash): add core crash workflow service`
- `94ebef70` `refactor(crash): consume core crash workflow prompt service`

If the next engineer wants to understand the current extraction shape, reading those commits in order is the fastest path.

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
2. use `FRONTEND_MIGRATION_PLAN.md` as the working migration brief
3. treat the launch login-orchestration blocker as mostly addressed and begin a small shell-replacement / frontend-contract phase
4. keep `ModLaunch.vb` cleanup incremental: only peel off remaining step-local adapter glue or prompt/shell wrappers when they materially simplify a new shell consumer
5. focus the next migration spike on replacing or paralleling a narrow launcher surface while continuing to reuse `PCL.Core` launch/startup/crash services
6. if additional workflow logic must move before a frontend cutover, keep moving it into `PCL.Core` services instead of duplicating it in the launcher
7. keep validating with the Foundation test suite and Foundation API scan whenever new shared logic is moved

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
- frontend migration is now mostly blocked by view/shell replacement and remaining launcher workflow/UI entanglement, not by runtime-core portability

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

## Phase Call

Do not treat the project as ready for a true “next phase” frontend-shell replacement yet.

It is ready for handoff to another engineer, but the recommended near-term phase is still:

1. finish the last high-value launcher workflow extraction slices
2. then start a narrow shell-migration spike

## One-Line Summary

Wave 2 is complete and a substantial Wave 3 cleanup set is landed: the runtime/core portability seams are stabilized, launch/startup/crash/bootstrap shell policies now have core-side services, Foundation remains headless and macOS-valid, and the next engineer should finish the remaining `ModLaunch.vb` login execution/orchestration extraction before attempting a real frontend-shell cutover.
