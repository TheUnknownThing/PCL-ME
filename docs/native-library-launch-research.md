# Native Library Launch Research

Last updated: 2026-04-09

## Scope

This note records the current research status around Minecraft native-library handling in the Avalonia launch path, plus the implementation plan for fixing the whole class of related multi-platform failures.

## Current Findings

### 1. HMCL treats natives as a complete launch pipeline

Relevant code:

- `HMCLCore/src/main/java/org/jackhuang/hmcl/launch/DefaultLauncher.java`
- `HMCLCore/src/main/java/org/jackhuang/hmcl/game/Library.java`
- `HMCLCore/src/main/java/org/jackhuang/hmcl/game/ExtractRules.java`
- `HMCL/src/main/java/org/jackhuang/hmcl/util/NativePatcher.java`

Observed behavior:

- HMCL resolves native classifiers from manifest `natives` / `downloads.classifiers`.
- HMCL downloads native jars as required launch artifacts.
- HMCL decompresses native jars into the selected natives directory before starting the game process.
- HMCL honors per-library extract exclusion rules.
- HMCL applies platform-specific compatibility hardening:
  - ASCII-safe natives path workaround on older Linux/macOS LWJGL
  - unsupported-platform native replacement via `NativePatcher`
  - platform and architecture compatibility logic centered on the target Java runtime

### 2. Avalonia currently only carries a natives directory string through launch

Relevant code:

- `PCL.Frontend.Avalonia/Workflows/FrontendLaunchCompositionService.cs`
- `PCL.Frontend.Avalonia/Workflows/FrontendShellActionService.cs`
- `PCL.Core/Minecraft/Launch/MinecraftLaunchNativesSyncService.cs`

Observed behavior:

- Launch composition resolves a natives directory and injects it into JVM arguments.
- Launch startup does not currently call `MinecraftLaunchNativesSyncService`.
- Launch required-artifact collection currently only includes `downloads.artifact`, not native classifiers.
- `MinecraftLaunchNativesSyncService` currently only extracts `.dll`, so Linux/macOS native libraries are skipped entirely.

Important implication:

- A Linux failure such as `Failed to locate library: liblwjgl.so` is expected if the native jar is not unpacked, or if unpacking is attempted but still filters out `.so`.

### 3. PCL-CE already has pieces of the missing pipeline, but they are split

Relevant code:

- `PCL.Frontend.Avalonia/Workflows/FrontendInstanceRepairService.cs`
- `PCL.Core/Minecraft/Launch/MinecraftLaunchNativesSyncService.cs`

Observed behavior:

- Repair code already knows how to resolve native-classifier downloads from `natives` / `downloads.classifiers`.
- Launch code does not currently reuse that logic.
- A native sync service already exists, but it is not wired into Avalonia launch and is not cross-platform yet.

## Working Hypothesis

The current native-library failures are not one bug. They come from the absence of one shared native-launch pipeline. The launch path currently lacks:

- native classifier artifact planning,
- pre-launch native extraction,
- cross-platform extraction behavior,
- and platform-aware native compatibility hardening.

## Patch Plan

### Phase A

Repair the native-launch baseline so launch works correctly on supported platforms.

- Include native-classifier jars in launch required artifacts.
- Introduce a native-sync plan in launch composition.
- Execute native sync before the Java process starts.
- Upgrade native extraction from `.dll`-only to cross-platform extraction.
- Honor manifest `extract.exclude` rules.

Expected result:

- Linux/macOS/Windows launches can actually populate `${natives_directory}` before startup.
- Failures such as missing `liblwjgl.so` become launch-preparation errors instead of runtime linker crashes.

Status: in progress

### Phase B

Unify native artifact selection and platform matching.

- Move native-classifier resolution into a shared launch-native planner.
- Stop duplicating native resolution logic between repair and launch.
- Base native selection on the selected Java runtime platform where needed, not only the launcher process architecture.

Expected result:

- launch, repair, and later diagnostics all agree on which native archives belong to the selected runtime/platform combination.

Status: pending

### Phase C

Add HMCL-style compatibility hardening for non-default platforms.

- Add ASCII-safe temporary native-path handling for older Linux/macOS LWJGL.
- Introduce optional native replacement support for unsupported or translation-based platforms.
- Add platform-specific warnings where the launcher can help but Mojang does not officially support the combination.

Expected result:

- older LWJGL and ARM/translated-platform native problems stop failing in ad-hoc ways and become controlled compatibility behavior.

Status: pending

### Phase D

Improve diagnostics.

- Record selected native archives, extraction target, and exclusion rules in launch diagnostics.
- Surface native preparation failures clearly in launch logs and inspection output.

Expected result:

- future native-library regressions become easy to trace without reproducing them blindly.

Status: pending
