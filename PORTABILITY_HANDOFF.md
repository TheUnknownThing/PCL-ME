# PCL-CE Portability Handoff

## Project Context

`PCL-CE` is a Windows-first Minecraft launcher with:

- a WPF/VB frontend in `Plain Craft Launcher 2`
- a mixed C# runtime/core in `PCL.Core`

The long-term direction is to make the launcher runnable on Linux and macOS as well as Windows. The current codebase is still fundamentally Windows-bound because it targets `net8.0-windows`, uses WPF heavily, and still contains Windows-specific runtime assumptions around UI, registry, proxy handling, and platform/process services.

## Big Goal

The portability roadmap is:

1. Create `PCL.Core.Foundation` as a pure headless `net8.0` library.
2. Keep `PCL.Core` as the current Windows adapter/runtime layer.
3. Introduce runtime abstractions for paths, config, network/proxy, Java discovery, and process/platform behavior.
4. Replace the WPF/VB UI only after the runtime boundary is stable.

Target shape:

- `PCL.Core.Foundation`
  - pure `net8.0`
  - no WPF
  - no Win32/Registry/WMI/PInvoke
  - reusable on macOS/Linux/Windows
- `PCL.Core`
  - Windows-specific adapter/runtime layer for current launcher behavior

## What Wave 1 Has Already Done

The first extraction chunk is now physically split into a new headless project:

- `PCL.Core.Foundation/PCL.Core.Foundation.csproj`
- `PCL.Core.Foundation.Test/PCL.Core.Foundation.Test.csproj`
- solution wiring updated in `Plain Craft Launcher 2.slnx`
- `PCL.Core` now references `PCL.Core.Foundation`

Moved into `PCL.Core.Foundation`:

- logging primitives and file logger
- non-WPF utility and extension helpers
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

Kept in Windows `PCL.Core`:

- `App.*`
- `App.IoC`
- `App.Essentials`
- `ConfigService`
- `Paths`
- `Basics`
- `Utils.OS`
- WPF and UI code
- network service, proxy, and lifecycle-bound runtime pieces

## Logging Compatibility Seam

The logging boundary was intentionally split so Foundation does not reach back into the Windows runtime:

- `PCL.Core.Foundation/Logging/LogWrapper.cs` exposes `AttachLogger(Logger logger)`
- `PCL.Core/Logging/LogService.cs` owns runtime logger creation and attaches it
- `LogWrapper.CurrentLogger` is preserved for existing frontend/runtime call sites

This compatibility shim is important. Keep it intact until the broader runtime abstraction work is ready.

## Current Verified Status

This repository now has a real build pass on macOS:

- installed SDKs:
  - .NET SDK `10.0.201` for builds
  - .NET SDK `8.0.419` side-by-side so plain `dotnet test` can run `net8.0` tests
- repo pin:
  - `global.json` pins SDK `10.0.201`

Verified successfully on this machine:

- `dotnet restore "Plain Craft Launcher 2.slnx"`
- `dotnet build PCL.Core.Foundation/PCL.Core.Foundation.csproj -c Debug`
- `dotnet build PCL.Core.Foundation.Test/PCL.Core.Foundation.Test.csproj -c Debug`
- `dotnet test PCL.Core.Foundation.Test/PCL.Core.Foundation.Test.csproj -c Debug --no-build`
- `dotnet build PCL.Core/PCL.Core.csproj -c Debug`

Foundation regression scan is clean:

- no `System.Windows`
- no `Microsoft.Win32`
- no `DllImport`
- no `LibraryImport`
- no `PCL.Core.App`
- no `PCL.Core.UI`

## Wave 1 Compile Fallout That Was Fixed

The first real SDK build exposed a few issues that are already fixed:

- `EncodingDetector` BOM detection was incorrectly pattern-matching fixed-length arrays and missing valid BOM-prefixed payloads.
- `EncodingDetector` UTF-8 fallback validation was not reading the stream sample before round-tripping.
- `SnapLiteVersionControl.CheckVersion()` returned the inverse of the expected result.
- file/folder validation depended on host OS path rules instead of preserving Windows launcher semantics inside Foundation.
- moved Foundation tests had stale Windows-specific fixtures and no legacy code-page registration.

Those fixes were applied without widening Wave 1 scope.

## What The Next Engineer Should Do

Stay focused on Wave 2 runtime abstractions, not UI migration.

Priority targets:

1. `Paths` and app environment abstraction
2. configuration storage/service abstraction
3. network and proxy abstraction
4. Java discovery and process/platform service abstraction

The goal of Wave 2 is to remove Windows assumptions like:

- registry-backed proxy behavior
- lifecycle-coupled config/UI error handling
- `java.exe`-only discovery logic
- Windows-only process/platform assumptions

But current Windows behavior should keep working through adapters in `PCL.Core`.

## Important Non-Goals Right Now

Do not do these yet unless the runtime boundary is already stable:

- replace the WPF/VB frontend
- rename public namespaces from `PCL.Core.*` to `PCL.Core.Foundation.*`
- move `ConfigService` as-is
- move current `IO.Net` as-is
- move `Utils.Secret` as-is

## Known Risks / Sticky Areas

- `ConfigService` still mixes storage behavior with lifecycle shutdown and UI popup handling.
- `IO.Net` still depends on lifecycle and Windows proxy monitoring behavior.
- `Utils.Secret` still depends on app state and Windows-specific crypto/device identity assumptions.
- The WPF/VB frontend remains the biggest blocker to actual cross-platform launcher support.

## One-Line Summary

The repo is now past the “the split exists on paper” stage: the headless foundation project is real, it builds and tests on macOS, the Windows layer still builds, and the next engineer should treat the immediate mission as “start Wave 2 runtime abstractions without breaking the new Foundation boundary.”
