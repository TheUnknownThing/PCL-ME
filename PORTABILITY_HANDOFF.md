# PCL-CE Portability Handoff

## Project Context

`PCL-CE` is a Windows-first Minecraft launcher with:

- a WPF/VB frontend in `Plain Craft Launcher 2`
- a mixed C# runtime/core in `PCL.Core`

The long-term direction is still:

1. make the runtime/core portable first
2. keep current Windows launcher behavior working through adapters
3. replace the WPF/VB frontend only after the runtime boundary is stable

The repo is no longer at the “portability is just an idea” stage. There is now a real headless `PCL.Core.Foundation` project, it builds and tests on macOS, and Wave 2 runtime/platform extraction is now complete.

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

Current Foundation test count:

- `93/93` passing

Current Foundation regression scan is clean:

- no `System.Windows`
- no `Microsoft.Win32`
- no `DllImport`
- no `LibraryImport`

## Recent Checkpoint Commits

This is the meaningful history for the current portability work:

- `ab55901f` `refactor(paths): extract portable app path layout into foundation`
- `ad18ee67` `refactor(config): move headless config storage into foundation`
- `633c41a8` `refactor(network): split proxy core from windows registry adapter`
- `37aa3d5e` `test(portability): add regression coverage and rerun mac build matrix`
- `91428de6` `refactor(java): extract portable java runtime and scanning core`
- `9422e395` `test(java): add portable runtime and scanner coverage`

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

The next engineer should treat Wave 2 as finished and move to the next boundary, not reopen the runtime seams that are now stable.

Recommended order:

1. keep `PCL.Core.Foundation` stable and avoid leaking UI/Win32 concerns back into it
2. decide whether the next phase is:
   - Wave 3 launcher/runtime integration cleanup, or
   - frontend migration planning
3. if continuing portability work, focus on launcher-side WPF/VB OS dependencies rather than runtime-core extraction
4. keep validating with the Foundation test suite and Foundation API scan whenever new shared logic is moved

## Important Non-Goals Right Now

Do not do these yet unless the runtime boundary is already stable:

- replace the WPF/VB frontend
- rename public namespaces from `PCL.Core.*` to `PCL.Core.Foundation.*`
- move `ConfigService` wholesale “as-is”
- move `Utils.Secret` wholesale “as-is”
- try to make the whole launcher runnable cross-platform before the runtime seams are done

## Known Risks / Sticky Areas

- `ConfigService` is still in `PCL.Core`; only the storage/runtime seam is headless
- Java launch and the launcher-side VB/WPF flows still contain Windows assumptions even though the runtime/discovery core is portable
- `Utils.Secret` still blocks a truly headless secure config/auth story and remains explicitly deferred
- the frontend is still WPF/VB and therefore still the biggest blocker to an actually cross-platform launcher binary

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

## One-Line Summary

Wave 2 is complete: the runtime/core portability seams are now extracted and stabilized, Windows-specific runtime behavior lives behind explicit adapters in `PCL.Core`, Foundation remains headless and macOS-valid, and the next phase should move up to launcher-side or frontend-level portability work rather than further core-runtime extraction.
