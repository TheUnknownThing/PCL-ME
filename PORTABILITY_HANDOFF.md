# PCL-CE Portability Handoff

## Project Context

`PCL-CE` is a Windows-first Minecraft launcher with:

- a WPF/VB frontend in `Plain Craft Launcher 2`
- a mixed C# runtime/core in `PCL.Core`

The long-term direction is still:

1. make the runtime/core portable first
2. keep current Windows launcher behavior working through adapters
3. replace the WPF/VB frontend only after the runtime boundary is stable

The repo is no longer at the “portability is just an idea” stage. There is now a real headless `PCL.Core.Foundation` project, it builds and tests on macOS, and several Wave 2 runtime seams are already extracted.

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

## What Still Remains For Wave 2

Wave 2 is not fully done yet.

The biggest remaining runtime/platform work is now concentrated in:

1. process/platform service abstraction
2. remaining `Utils.OS` Windows-specific helpers
3. remaining Windows-bound app/runtime services that still directly inspect OS/registry/process state

The most obvious remaining hotspots:

- `PCL.Core/Utils/OS/ProcessInterop.cs`
  - uses Windows admin checks, WMI, registry GPU preference logic, `TASKKILL.EXE`
- `PCL.Core/Utils/OS/SystemTheme.cs`
- `PCL.Core/UI/Theme/SystemThemeHelper.cs`
- `PCL.Core/Utils/OS/RegistryChangeMonitor.cs`
- `PCL.Core/App/Essentials/TelemetryService.cs`
  - still uses registry reads
- some startup/diagnostic helpers that still read OS/process state directly
- `Utils.Secret`
  - still depends on Windows-specific crypto/device assumptions and remains explicitly out of scope for now

## Recommended Next Steps

The next engineer should stay focused on runtime extraction, not UI migration.

Recommended order:

1. extract process/platform abstractions out of `ProcessInterop`
   - separate pure process start/kill/command querying from Windows-only admin/WMI/registry behavior
2. extract remaining non-UI OS/environment helpers that can become Foundation-safe
3. isolate theme/registry observers and telemetry OS probes behind Windows adapters
4. do another macOS validation pass and Foundation scan after each slice

## Important Non-Goals Right Now

Do not do these yet unless the runtime boundary is already stable:

- replace the WPF/VB frontend
- rename public namespaces from `PCL.Core.*` to `PCL.Core.Foundation.*`
- move `ConfigService` wholesale “as-is”
- move `Utils.Secret` wholesale “as-is”
- try to make the whole launcher runnable cross-platform before the runtime seams are done

## Known Risks / Sticky Areas

- `ConfigService` is still in `PCL.Core`; only the storage layer is headless so far
- Java launch/launcher UI still has many Windows-specific assumptions in VB/WPF call sites even though discovery/runtime core is more portable now
- `ProcessInterop` is still one of the densest Windows-specific runtime clusters
- `Utils.Secret` still blocks a truly headless secure config/auth story
- the frontend is still WPF/VB and therefore still the biggest blocker to an actually cross-platform launcher binary

## Working Rules For The Next Engineer

- preserve compatibility-first behavior
- do not break existing static/public entry points unless there is no alternative
- keep adding small checkpoint commits after each green validation slice
- run `dotnet test PCL.Core.Foundation.Test/PCL.Core.Foundation.Test.csproj -c Debug`
- run `dotnet build PCL.Core/PCL.Core.csproj -c Debug`
- rerun the Foundation Windows-API scan after each extraction chunk

## One-Line Summary

The project is now partway through Wave 2: paths, config storage, proxy/network seams, and the Java runtime/discovery core are extracted into Foundation, the repo is green on macOS validation, and the next engineer should focus on the remaining process/platform and OS-bound runtime helpers without touching the UI migration yet.
