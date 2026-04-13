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

Observed behavior before the current fixes:

- Launch composition resolved a natives directory and injected it into JVM arguments.
- Launch startup did not call `MinecraftLaunchNativesSyncService`.
- Launch required-artifact collection only included `downloads.artifact`, not native classifiers.
- `MinecraftLaunchNativesSyncService` only extracted `.dll`, so Linux/macOS native libraries were skipped entirely.

Important implication:

- A Linux failure such as `Failed to locate library: liblwjgl.so` is expected if the native jar is not unpacked, or if unpacking is attempted but still filters out `.so`.

### 3. PCL-ME already has pieces of the missing pipeline, but they are split

Relevant code:

- `PCL.Frontend.Avalonia/Workflows/FrontendInstanceRepairService.cs`
- `PCL.Core/Minecraft/Launch/MinecraftLaunchNativesSyncService.cs`

Observed behavior after the first launch fixes:

- Launch now plans native-classifier downloads as required artifacts.
- Launch now runs native sync before starting Java.
- Native extraction is now cross-platform and honors `extract.exclude`.
- Launch runtime selection and setup UI now share the retained Java inventory stack.

Remaining split:

- Repair and launch still use separate manifest-reading paths.
- Inspection and diagnostics still do not report native planning in the same depth HMCL does.

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

Implemented:

- `30531d83` `Prepare and extract launch native libraries`

Status: completed

### Phase B

Unify native artifact selection and platform matching.

- Move native-classifier resolution into a shared launch-native planner.
- Stop duplicating native resolution logic between repair and launch.
- Base native selection on the selected Java runtime platform where needed, not only the launcher process architecture.

Expected result:

- launch, repair, and later diagnostics all agree on which native archives belong to the selected runtime/platform combination.

Implemented in this slice:

- carry Java architecture through `FrontendJavaRuntimeSummary`
- base launch required artifacts, native archive selection, and manifest rule `os.arch` checks on the selected Java runtime architecture when available
- base classpath library filtering on the resolved target Java architecture instead of the Avalonia process architecture
- base Mojang runtime-download platform selection on the target Java architecture instead of `RuntimeInformation.ProcessArchitecture`
- base JSON launch-argument `os.arch` evaluation on the selected Java runtime bitness instead of the launcher process bitness

Still remaining:

- remove the remaining duplication between repair manifest traversal and launch manifest traversal
- align inspection output and diagnostics with the new target-runtime-native planning
- decide whether manifest-summary parsing should also become fully runtime-contextual, or stay as metadata-only parsing

Status: in progress

### Phase C

Add HMCL-style compatibility hardening for non-default platforms.

- Add ASCII-safe temporary native-path handling for older Linux/macOS LWJGL.
- Introduce optional native replacement support for unsupported or translation-based platforms.
- Add platform-specific warnings where the launcher can help but Mojang does not officially support the combination.

Expected result:

- older LWJGL and ARM/translated-platform native problems stop failing in ad-hoc ways and become controlled compatibility behavior.

Implemented in this slice:

- detect manifest JVM arguments that redirect `-Djava.library.path` to `${natives_directory}/subdir`
- extract native archives into that effective native target instead of always extracting to the base natives directory
- create a stable ASCII-safe alias path on Linux/macOS for pre-1.19 native loading when the chosen natives directory is non-ASCII

Still remaining:

- native replacement / patching for unsupported platforms (`NativePatcher`-style)
- explicit compatibility prompts for translated / launcher-supported but not Mojang-supported platform combinations

Status: in progress

### Phase D

Improve diagnostics.

- Record selected native archives, extraction target, and exclusion rules in launch diagnostics.
- Surface native preparation failures clearly in launch logs and inspection output.

Expected result:

- future native-library regressions become easy to trace without reproducing them blindly.

Implemented in this slice:

- startup session summaries now record the real natives folder used by launch instead of a fixed placeholder path
- startup session summaries now include native search path, extraction target, ASCII alias path, and native archive count
- inspection renderers now surface native alias, extraction target, archive count, and search path in launch summaries
- native sync logs now record each native archive path and its extract exclusion rules, and those logs are appended into the written startup session summary

Still remaining:

- surface native preparation details directly in the interactive launch prompt / activity stream when preparation succeeds
- carry the richer native diagnostics through inspection/replay plans, not only live launch composition and session summaries

Status: in progress

## Current Launch-State Summary

What Avalonia launch now does:

- chooses Java with compatibility-aware selection and retained inventory data
- plans native classifier downloads into the required-artifact list
- extracts native jars before launch on Linux, macOS, and Windows
- respects extract exclusion rules
- resolves launch-time native rules and classifiers against the selected Java runtime architecture when known
- resolves launch-time classpath libraries against the target Java architecture rather than the launcher process architecture
- evaluates JSON argument `os.arch` rules against the selected Java runtime bitness
- extracts natives into the effective `java.library.path` subdirectory when the manifest requests `${natives_directory}/...`
- provides an ASCII-safe alias path for older Linux/macOS LWJGL when the real natives path is non-ASCII

What is still missing compared with HMCL:

- native replacement / patching for unsupported platforms (`NativePatcher`-style behavior)
- richer live UI diagnostics for native preparation and unsupported-platform fallback behavior
