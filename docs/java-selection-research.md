# Java Selection Research

Last updated: 2026-04-09

## Scope

This note records the current research status around Java discovery and Java selection in PCL-ME, plus the next investigation steps. It is intended to preserve context if the conversation gets compressed.

## Current PCL-ME Findings

### 1. There are currently two Java-selection layers

- The older, fuller Java inventory and auto-selection logic lives in `PCL.Core` and `PCL.Core.Foundation`, centered around `JavaService` and `JavaManager`.
- The Avalonia launch path uses `FrontendLaunchCompositionService.ResolveJavaRuntime(...)` to pick the runtime that will actually be used for launch composition.
- These two layers do not currently behave the same.

### 2. The retained `JavaManager` still has a compatibility-aware selector

Relevant code:

- `PCL.Core/Minecraft/Java/JavaService.cs`
- `PCL.Core.Foundation/Minecraft/Java/JavaManager.cs`
- `PCL.Core.Foundation/Minecraft/Java/DefaultJavaInstallationEvaluator.cs`
- `PCL.Core.Foundation/Minecraft/Java/Parser/CommandJavaParser.cs`
- `PCL.Core.Foundation/Minecraft/Java/Scanner/*.cs`

Observed behavior:

- Startup builds a `JavaManager` with a composite parser:
  - `CommandJavaParser`
  - `PeHeaderParser`
- Startup scans these sources:
  - Windows registry
  - default filesystem roots
  - `PATH`
  - Microsoft Store runtime
  - `where` / `which`
- Discovered paths are normalized and deduplicated.
- Default enablement rejects:
  - JRE > 8
  - Java whose bitness mismatches OS bitness
  - runtimes missing expected runtime markers
- `SelectSuitableJavaAsync(minVersion, maxVersion)` filters by:
  - still available
  - enabled
  - version in acceptable range
- Then it sorts candidates by:
  1. lower Java major version first
  2. JDK before JRE
  3. brand enum order
  4. higher patch version first

Important implication:

- This selector is designed to choose a Java that fits the instance's required min/max version window.

### 3. Multi-platform discovery work was added in the retained manager path

Relevant code:

- `PCL.Core.Foundation/Minecraft/Java/Parser/CommandJavaParser.cs`
- `PCL.Core.Foundation/Minecraft/Java/Scanner/DefaultPathsScanner.cs`
- `PCL.Core.Foundation/Minecraft/Java/Scanner/PathEnvironmentScanner.cs`
- `PCL.Core.Foundation/Minecraft/Java/Scanner/WhereCommandScanner.cs`
- `PCL.Core.Foundation.Test/Java/JavaPortabilityTest.cs`

Observed behavior:

- `CommandJavaParser` extracts metadata from `java -XshowSettings:properties -version`.
- Non-Windows executable names are handled as `java` / `javac` instead of `.exe`.
- `DefaultPathsScanner` switches roots by platform:
  - Windows: AppData, LocalAppData, UserProfile, `Program Files`, root keyword search
  - macOS: JavaVirtualMachines locations, `/opt`
  - Linux: `/usr/lib/jvm`, `/usr/java`, `/opt`
- `PathEnvironmentScanner` uses runtime path separator and runtime-specific executable names.
- `WhereCommandScanner` uses `which -a java` on non-Windows.

### 4. Launch-time Avalonia resolution is currently much simpler

Relevant code:

- `PCL.Frontend.Avalonia/Workflows/FrontendLaunchCompositionService.cs`
- `PCL.Core/Minecraft/Launch/MinecraftLaunchJavaRequirementService.cs`
- `PCL.Core/Minecraft/Launch/MinecraftLaunchJavaWorkflowService.cs`
- `PCL.Core/Minecraft/Launch/MinecraftLaunchJavaPromptService.cs`

Observed launch-time algorithm:

1. Read the instance manifest and compute Java version requirements.
2. Build a Java workflow with min/max acceptable versions and prompt/download metadata.
3. Resolve the selected Java source:
   - instance `VersionArgumentJavaSelect` if present
   - otherwise global `LaunchArgumentJavaSelect`
4. If an explicit path is resolved:
   - use matching stored entry if enabled
   - otherwise use the file directly if it exists
5. If there is no explicit path:
   - use the first enabled stored Java entry
   - otherwise use bundled launcher runtime
   - otherwise probe host Java from common paths / `which` / `java`
6. If nothing is found, prompt for download.

Important implication:

- The current Avalonia launch path does not call the compatibility-aware `JavaManager.SelectSuitableJavaAsync(...)`.
- It computes `MinimumVersion` and `MaximumVersion`, but the actual runtime selection path does not use that range to filter or rank the automatic choice.
- Instance setting `auto` currently collapses into the fallback path rather than a dedicated compatibility-aware auto-selection routine.

### 5. There are likely data-flow regressions in the Avalonia path

Observed mismatch A: stored Java list shape

- Stored Java inventory is persisted as `JavaStorageItem[]` with fields:
  - `Path`
  - `IsEnable`
  - `Source`
- Avalonia parsers for launch and instance composition currently expect nested fields such as:
  - `Installation.JavaExePath`
  - `Installation.Version`
  - `Installation.Is64Bit`
  - `IsEnabled`

Implication:

- The current launch/instance parsers will often fail to reconstruct the stored Java list from the actual persisted format.

Observed mismatch B: config location

- Setup and surface actions read/write `LaunchArgumentJavaUser` in local config.
- `FrontendInstanceCompositionService` currently reads `LaunchArgumentJavaUser` from shared config.

Implication:

- The instance setup page can diverge from the real stored runtime list.

## Working Hypothesis

The current "Java selection still has some problem" is likely not one isolated bug. The launch path appears to have regressed from the richer legacy-compatible Java selection flow into a simpler resolver that:

- may not successfully parse the stored Java inventory,
- does not perform compatibility-aware auto-selection,
- and can fall back to bundled or host Java without checking the required version window as strictly as the retained manager logic intended.

## Immediate Research Plan

### Step 1

Inspect HMCL's Java discovery and selection algorithm in `/home/wano/workspace/HMCL`.

Status: completed

## HMCL Findings

### 1. HMCL keeps one authoritative Java inventory model

Relevant code:

- `HMCL/src/main/java/org/jackhuang/hmcl/java/JavaManager.java`
- `HMCLCore/src/main/java/org/jackhuang/hmcl/java/JavaRuntime.java`
- `HMCL/src/main/java/org/jackhuang/hmcl/setting/VersionSetting.java`

Observed behavior:

- HMCL stores Java runtimes as `JavaRuntime` objects keyed by real executable path.
- Inventory is initialized once, cached in memory, exposed as `allJava`, and refreshed as a whole.
- User-added Java paths and disabled Java paths are separate config inputs into discovery, not alternate data shapes interpreted later.

Important implication:

- The launch path consumes the same runtime model that discovery produced.
- HMCL does not appear to have a separate launch-only parser for persisted Java JSON with a mismatched shape.

### 2. HMCL discovery is platform-aware and architecture-aware

Relevant code:

- `HMCL/src/main/java/org/jackhuang/hmcl/java/JavaManager.java`
- `HMCL/src/main/java/org/jackhuang/hmcl/java/JavaInfoUtils.java`

Observed behavior:

- HMCL explicitly models platform compatibility instead of only matching the current OS exactly.
- It permits architecture fallbacks that are known to work:
  - Windows x64 may use x86 Java
  - Windows ARM64 may use x64 or x86 Java on recent systems
  - Linux x64 may use x86 Java
  - macOS ARM64 may use x64 Java
- Discovery searches:
  - HMCL-managed Java repositories
  - registry / Program Files on Windows
  - `/usr/java`, `/usr/lib/jvm`, `/usr/lib32/jvm`, `/usr/lib64/jvm`, `~/.sdkman/candidates/java` on Linux
  - JavaVirtualMachines and Homebrew locations on macOS
  - Minecraft bundled runtimes
  - launcher cache repository runtimes
  - `PATH`
  - `HMCL_JRES`
  - `~/.jdks`
  - user-added Java paths
  - current running Java

Important implication:

- HMCL has broader and more architecture-conscious multi-platform coverage than the current PCL-ME launch resolver.

### 3. HMCL caches Java metadata safely

Relevant code:

- `HMCL/src/main/java/org/jackhuang/hmcl/java/JavaManager.java`

Observed behavior:

- HMCL writes a `javaCache.json`.
- Cache entries are keyed by real executable path plus a derived cache key based on launcher binary metadata and either:
  - the hash of the `release` file, or
  - `rt.jar` metadata for older layouts
- Cache is invalidated when compatibility or key checks no longer match.

Important implication:

- HMCL avoids reparsing every candidate every time, but still ties cache validity to the actual runtime contents.

### 4. HMCL metadata extraction is cross-platform and executable-driven

Relevant code:

- `HMCL/src/main/java/org/jackhuang/hmcl/java/JavaInfoUtils.java`

Observed behavior:

- HMCL runs the target Java executable with a helper entrypoint and reads structured JSON-like environment output back into:
  - `os.arch`
  - `java.version`
  - `java.vendor`
- Java architecture is therefore determined from the target runtime itself, not inferred from the host process.

Important implication:

- This is stronger than the current PCL-ME host fallback probe, which only parses `java -version` output for major version and assumes Java bitness from the launcher process.

### 5. HMCL separates discovery from compatibility constraints

Relevant code:

- `HMCLCore/src/main/java/org/jackhuang/hmcl/game/JavaVersionConstraint.java`
- `HMCL/src/main/java/org/jackhuang/hmcl/java/JavaManager.java`

Observed behavior:

- HMCL does not rely on a single min/max range only.
- Instead, it evaluates each candidate Java against a set of version constraints.
- Constraints are divided into:
  - mandatory constraints
  - suggested constraints
- Examples include:
  - vanilla minimum Java by Minecraft version
  - `javaVersion` in version JSON
  - Forge preferred Java bands by game era
  - Cleanroom-specific Java rules
  - old LaunchWrapper forcing Java 8
  - Linux native-loading restrictions for old versions on Java 9+
  - ARM/x86 compatibility caveats
  - known ModLauncher/JDK bug windows

Important implication:

- HMCL’s selection algorithm is rule-based, not just version-window-based.
- This is why it handles multi-platform edge cases better.

### 6. HMCL auto-selection returns the best fully compatible runtime, or the best mandatory-only runtime

Relevant code:

- `HMCL/src/main/java/org/jackhuang/hmcl/java/JavaManager.java`

Observed behavior:

- `findSuitableJava(...)` iterates over Java candidates.
- It first filters by architecture suitability:
  - usually exact system architecture
  - special x86 fallback for old versions on Windows/macOS ARM64
- For each candidate it records:
  - whether any mandatory constraints are violated
  - whether any suggested constraints are violated
- It tracks two best candidates:
  - `mandatory`: passes all mandatory constraints
  - `suggested`: passes all mandatory and suggested constraints
- Final selection is:
  - `suggested` if present
  - otherwise `mandatory`

Its internal tie-breaker chooses:

1. lower Java major first, described in code as being closer to the game's recommended Java
2. if same major, higher patch version first

Important implication:

- HMCL intentionally prefers the lowest sufficient major, not the newest available Java.
- That is similar in spirit to PCL-ME's retained manager logic, but HMCL's candidate filtering is much richer.

### 7. HMCL keeps explicit user choice distinct from auto mode

Relevant code:

- `HMCL/src/main/java/org/jackhuang/hmcl/setting/VersionSetting.java`

Observed behavior:

- Version setting modes are explicit:
  - `DEFAULT`
  - `AUTO`
  - `VERSION`
  - `DETECTED`
  - `CUSTOM`
- `AUTO` always calls `JavaManager.findSuitableJava(...)`.
- `CUSTOM` resolves the exact given path.
- `VERSION` filters all runtimes to a requested major and then still runs `findSuitableJava(...)`.
- `DETECTED` prefers a previously detected exact version/path, but falls back to `findSuitableJava(...)`.

Important implication:

- HMCL does not collapse all non-explicit cases into "first enabled Java".
- Every mode is still mediated by the central compatibility-aware selector unless the user truly forces a custom path.

### 8. HMCL launch flow validates the chosen Java and can recover

Relevant code:

- `HMCL/src/main/java/org/jackhuang/hmcl/game/LauncherHelper.java`

Observed behavior:

- If the selected Java is missing or violates mandatory constraints:
  - HMCL tries to find a better compatible Java automatically
  - if that succeeds, it can switch the setting back to auto
  - if not, it may download the required Mojang runtime
  - if no safe recovery exists, it presents a targeted error
- Suggested-constraint violations do not necessarily block launch, but they produce concrete advice.

Important implication:

- HMCL has a separate validation-and-recovery layer after basic selection.
- This prevents invalid explicit selections from silently remaining the final launch runtime.

## Current Comparison

### HMCL vs retained PCL-ME `JavaManager`

- Both prefer the lowest sufficient Java major instead of the newest Java.
- Both have platform-aware discovery work.
- HMCL is more advanced in architecture compatibility and in special-case constraints.
- HMCL keeps one authoritative runtime inventory and uses it directly for launch selection.

### HMCL vs current PCL-ME Avalonia launch path

- HMCL: central inventory model plus compatibility-aware selector.
- PCL-ME Avalonia: config/path resolver plus simple fallback chain.
- HMCL: rule-based mandatory/suggested constraints.
- PCL-ME Avalonia: computes requirements, but current runtime selection does not use them to score/filter auto choice.
- HMCL: explicit modes remain integrated with central selection logic.
- PCL-ME Avalonia: `auto` currently falls through to "first enabled / bundled / host".
- HMCL: validated recovery after bad selection.
- PCL-ME Avalonia: limited recovery and weaker metadata for host probe.

## Updated Working Hypothesis

The most promising repair direction for PCL-ME is not to keep expanding the current Avalonia fallback chain. It is to restore launch-time resolution onto a single authoritative Java inventory plus a compatibility-aware selector.

The retained PCL-ME `JavaManager` is closer to that direction already, but HMCL shows several missing pieces:

- richer constraint handling,
- better architecture compatibility rules,
- safer runtime metadata extraction,
- and a cleaner separation between explicit user choice, automatic choice, and post-selection recovery.

## Updated Next Steps

### Step 2

Compare HMCL's algorithm to:

- PCL-ME retained `JavaManager` logic
- PCL-ME current Avalonia launch resolver

Status: completed at a high level

### Step 3

Turn the comparison into a concrete PCL-ME repair plan.

Likely implementation direction:

- make launch-time auto selection use a central runtime inventory instead of ad hoc JSON parsing,
- fix the persisted Java list read/write mismatch,
- fix local/shared config mismatch for Java inventory,
- preserve explicit user path selection,
- and add compatibility-aware filtering before fallback to bundled or host Java.

## Patch Plan

### Phase 1

Repair the Java inventory data flow in Avalonia.

- Add one shared parser for stored Java entries that understands the real persisted shape:
  - `JavaStorageItem { Path, IsEnable, Source }`
- Keep a fallback reader for the older nested shape if present in old data.
- Use that shared parser in:
  - `FrontendLaunchCompositionService`
  - `FrontendInstanceCompositionService`
  - `FrontendSetupCompositionService`
  - Java settings surface actions that read/write `LaunchArgumentJavaUser`
- Fix `FrontendInstanceCompositionService` to read `LaunchArgumentJavaUser` from local config instead of shared config.

Expected result:

- setup page, instance page, and launch path will read the same Java inventory again.

Status: completed

### Phase 2

Repair launch-time automatic Java selection.

- Keep explicit user-selected Java path behavior stable in the first slice.
- For automatic selection:
  - choose among enabled stored Java entries,
  - require compatibility with the computed Java workflow min/max range,
  - rank candidates using the retained PCL-style order:
    1. lower sufficient major version first
    2. JDK before JRE
    3. brand preference
    4. higher patch version first
- Only if no compatible stored runtime exists:
  - try bundled Java
  - then try host Java
- For bundled/host fallback, require compatibility before accepting them as the automatic selection result.

Expected result:

- `auto` becomes compatibility-aware again instead of using "first enabled Java".

Status: completed

### Phase 3

Validate explicit Java selection and recover safely.

- Preserve the distinction between:
  - follow global Java setting
  - force auto selection
  - force a specific Java path
- If the instance setting is "follow global", defer to shared `LaunchArgumentJavaSelect` instead of collapsing to auto mode.
- If an explicit Java is selected:
  - keep it only when it is enabled and compatible with the computed Java workflow requirements
  - if `VersionAdvanceJava` is enabled for the instance, allow the explicit Java even when incompatible
  - otherwise recover by falling back to the normal automatic selection chain
- Do not blindly accept an explicit path whose version cannot be parsed unless compatibility override is enabled.

Expected result:

- explicit Java behaves like HMCL-style validated custom selection instead of bypassing compatibility checks,
- and "follow global" no longer drops the global Java selection.

Status: completed

### Phase 4

Improve host Java probing metadata.

- Parse enough information from host Java probe output to recover:
  - full Java version
  - major version
  - probable bitness when available
- Cache probed host runtimes as candidates, not as a single winner.

Expected result:

- automatic fallback to host Java becomes less lossy and less dependent on probe order.

Status: completed

### Phase 5

Follow-up work after the initial repair lands.

- Route Avalonia launch-time candidate discovery through a shared `JavaManager` inventory path that Avalonia can consume directly.
  - Completed in the current refactor by moving the retained `PeHeaderParser`, `RegistryJavaScanner`, and `MicrosoftStoreJavaScanner` into `PCL.Core.Foundation`, then exposing a shared `JavaManagerFactory.CreateDefault(...)`.
  - `PCL.Core.JavaService` now builds its manager through that factory with `StatesJavaStorage`.
  - Avalonia background discovery now builds the same retained manager through that factory, so launch-time candidate discovery uses the same parser / scanner stack as the retained path, including Windows registry and Microsoft Store sources.
- Portable scanner integration inside Avalonia is already implemented, but the initial version blocked shell construction by scanning synchronously during composition. That regression has been fixed by moving scan warm-up to the background and refreshing launch composition after cache population.
- Consider adding HMCL-style special constraints incrementally:
  - Linux legacy-native Java 8 restriction
  - ARM64/x86 compatibility rules
  - LaunchWrapper / ModLauncher special cases
- Reassess whether instance-level "ignore Java compatibility warning" needs dedicated UI feedback beyond the current runtime-selection override.

Status: in progress

## Open Design Questions

- Whether to adapt the retained PCL-ME `JavaManager` directly into the Avalonia launch path, or to create a new launch-oriented selector service that consumes the same runtime inventory.
- Whether PCL-ME should keep simple min/max version rules first, then add HMCL-style special constraints incrementally.
- How much HMCL-style architecture compatibility should be mirrored immediately, especially:
  - macOS ARM64 using x64 Java
  - Windows ARM64 using x64/x86 Java
  - Linux x64 permitting x86 Java where legacy versions need it
