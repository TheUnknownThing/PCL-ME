# PCL.Frontend.Avalonia

`PCL.Frontend.Avalonia` is the active cross-platform frontend for PCL-ME, built on top of the extracted startup / launch / crash workflow layer.

It targets plain `net10.0`, references `PCL.Core.Backend`, and carries the current `C# + Avalonia` desktop stack instead of the legacy WPF/VB.NET frontend.

The project now has two complementary faces:

- a CLI shell that still emits plan, run, and execute artifacts for reviewer-friendly inspection
- an Avalonia desktop shell that renders the portable startup, navigation, and prompt contracts as a real replacement-frontend surface

## What it proves

- a cross-platform Avalonia shell can consume the extracted startup, launch, and crash contracts
- the portable backend can drive both machine-readable output and shell-oriented transcript output
- the same prototype shell can materialize bootstrap, prerun, and crash-export artifacts in a real workspace through adapter-style file execution
- launch scenarios can now trace Authlib or Microsoft login request execution with inspectable request/response artifacts
- launch scenarios now also expose a portable Java runtime download plan, including the resolved runtime component and planned manifest files
- launch scenarios now also trace the launcher-style Java index and manifest request sources, and `execute` mode can materialize a stub runtime tree from the portable download workflow
- launch scenarios now also separate reused runtime files from actual download transfers so the shell can model partial-runtime recovery instead of only all-or-nothing downloads
- launch scenarios can now also model launcher-style batch-script export, including the export target, abort hint, and shell reveal behavior
- the shell can round-trip startup, launch, and crash inputs through `_inputs/*.json` snapshots and replay them later with `--input-root`
- the shell can now also round-trip a backend-driven frontend shell snapshot that includes startup prompts, top navigation, sidebar state, and utility surfaces
- the shell now also exposes a frontend prompt queue and a richer current-page surface contract with breadcrumbs and back-target semantics
- the avalonia now also renders route-specific page facts and sections from portable frontend page-content contracts instead of generic placeholder cards
- the avalonia can also derive best-effort host-backed startup, launch, and crash inputs with `--host-env true`
- the avalonia can now also boot a real desktop shell from those same portable contracts with `app`
- the desktop shell now also executes prompt-command actions through frontend adapters instead of only recording prompt intents
- the desktop shell now also scopes startup, launch, and crash prompt lanes to their actual lifecycle triggers instead of showing all prompt lanes at boot
- frontend concerns can be modeled as prompt decisions, file-work summaries, and process or shell transcripts without pulling workflow policy back into the launcher

## Commands

Default behavior stays compatibility-friendly:

- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- startup`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- launch legacy-forge`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- crash`

These emit `plan`-mode JSON by default.

Useful reviewer commands:

- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app --scenario legacy-forge`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app --host-env true`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- shell --mode plan --format text`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- shell --mode run --format text`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- shell --mode execute --format text --workspace /tmp/pcl-shell-nav`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- startup --mode run --format text`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- launch legacy-forge --mode run --format text --java-prompt download`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- launch legacy-forge --mode run --format text --java-prompt download --java-download-state failed`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- launch legacy-forge --mode run --format text --java-prompt abort`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- launch legacy-forge --mode run --format text --save-batch exports/Launch.bat`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- crash --mode run --format text --crash-action export`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- all legacy-forge --mode run --format text`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- launch legacy-forge --mode execute --format text --workspace /tmp/pcl-launch-avalonia`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- launch legacy-forge --mode execute --format text --workspace /tmp/pcl-launch-avalonia --java-download-state aborted`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- launch legacy-forge --mode execute --format text --workspace /tmp/pcl-launch-avalonia --save-batch exports/Launch.bat`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- crash --mode execute --format text --workspace /tmp/pcl-crash-avalonia`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- crash --mode execute --format text --workspace /tmp/pcl-crash-avalonia --export-path exports/demo-report.zip`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- all legacy-forge --mode execute --format text --workspace /tmp/pcl-shell-avalonia`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- launch modern-fabric --mode run --format text --input-root /tmp/pcl-launch-avalonia/_inputs/launch.json`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- launch modern-fabric --mode run --format text --host-env true`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- crash --mode execute --format text --workspace /tmp/pcl-host-crash --host-env true --export-path reports/host-report.zip`

## Options

- `app`
- `--mode plan|run|execute`
- `--format json|text`
- `--scenario modern-fabric|legacy-forge`
- `--host-env true|false`
- `--java-prompt download|abort`
- `--java-download-state finished|failed|aborted`
- `--save-batch /absolute/or/workspace-relative/Launch.bat`
- `--crash-action close|view-log|open-settings|export`
- `--workspace /absolute/or/relative/path`
- `--input-root /path/to/file/or/_inputs_directory`
- `--export-path /absolute/or/workspace-relative/archive.zip`

`execute` mode creates a workspace root automatically when `--workspace` is omitted.

## Desktop shell

The `app` command launches a real cross-platform desktop shell built with Avalonia.

Current desktop shell behavior:

- top-level routes, sidebar sections, and utility surfaces are rendered from `LauncherFrontendNavigationService`
- startup, launch, and crash prompt lanes are rendered from `LauncherFrontendPromptService`
- route-specific center-pane facts and sections are rendered from `LauncherFrontendPageContentService`
- route switching, shell back-navigation, and prompt dismissal stay in the frontend layer without copying legacy WPF event flow
- startup prompts now appear at boot, launch prompts appear from a launch attempt, and crash prompts appear from an explicit crash event trigger
- prompt choices now drive config persistence, route changes, crash export/log actions, Java runtime materialization, launch abort/continue state, and shell exit behavior
- the activity feed still records those actions so contract-driven behavior can be reviewed visually

The desktop shell is intentionally still a migration scaffold, not a full launcher:

- it renders portable shell surfaces and prompt-driven actions
- it now renders portable page-level summaries for launch, setup, tools, logs, and instance routes
- it still does not implement live backend execution or full page-specific production data
- it keeps policy in backend services and limits the frontend to composition, routing, and prompt interaction

## Packaging

The frontend now has a Track 6 packaging script that reuses the existing launcher icon and emits packaged builds for macOS, Linux, and Windows from the Avalonia project:

- `./PCL.Frontend.Avalonia/scripts/package-frontend.sh`
- default targets: `osx-arm64`, `linux-x64`, `win-x64`
- output root: `artifacts/frontend-packages/`

Typical usage:

```bash
./PCL.Frontend.Avalonia/scripts/package-frontend.sh
```

Override the defaults when needed:

```bash
APP_VERSION=2026.04.05 ./PCL.Frontend.Avalonia/scripts/package-frontend.sh osx-arm64
```

Packaging notes:

- macOS packaging emits `PCL-ME.app` plus a zip archive, and the app bundle launches the Avalonia shell with `app --host-env true`
- Linux packaging emits a tarball with the published frontend, a copied launcher icon, a launcher shell script, and a `.desktop` file that resolves relative to the extracted package folder
- Windows packaging emits a zip archive of the published frontend directory plus a `Launch PCL-ME.vbs` entry point that starts the Avalonia shell without changing the CLI binary behavior
- the script defaults to self-contained publish output; set `PUBLISH_MODE=framework-dependent` to switch modes

## Execute mode outputs

- shell execution writes startup prompt, navigation view, and navigation catalog artifacts so a replacement frontend can inspect the portable shell seam directly
- shell execution now also writes frontend prompt queue and page-surface artifacts so route composition can be validated without depending on legacy WPF state
- startup execution creates bootstrap directories, deletes seeded legacy log placeholders, and writes prompt/config artifacts
- launch execution can materialize `launcher_profiles.json`, `options.txt`, a generated launch batch file, and session summary artifacts
- launch execution now also materializes per-step login request/response artifacts under `_artifacts/login/...`
- launch execution now writes Java index/manifest request artifacts under `_artifacts/java-download/`, writes `_artifacts/java-download-plan.txt`, and materializes a stub runtime tree when the backend selects an automatic Java download path
- launch execution now also writes `_artifacts/java-download-transfer.txt`, seeds reused runtime files, and only materializes transfer files for the remaining Java download queue
- launch execution can now also model finished vs failed vs aborted Java download session transitions, including cleanup and refresh artifacts sourced from `MinecraftJavaRuntimeDownloadSessionService`
- launch execution with `--save-batch` now exports the batch script to the requested path, records the export shell policy, and stops before custom-command/process startup
- crash execution can stage sample input files and build a real crash zip archive via `MinecraftCrashExportArchiveService`
- crash execution records the selected archive destination in `_artifacts/crash-export-target.txt`, and `--export-path` can override the default workspace output path
- aborting the launch Java prompt in `execute` mode stops before prerun file work, so no launch artifacts are created
- each execution workspace also stores `_inputs/*.json` snapshots so the same shell inputs can be replayed later

## Input replay

- `--input-root` accepts either a single JSON file such as `launch.json` or a directory containing `shell.json`, `startup.json`, `launch.json`, or `crash.json`
- when `--input-root` is provided, the avalonia reuses those serialized inputs instead of the built-in defaults
- this makes it easy to execute once, tweak the saved JSON, and rerun the shell against file-backed state

## Host-backed inputs

- `--host-env true` tells the avalonia to derive startup, launch, and crash inputs from the current machine when `--input-root` is not provided
- this currently swaps in host paths, home/config/log locations, OS snapshot data, portable Java/process defaults, and best-effort detection of already-present Java runtime files so the shell transcript is closer to a real non-Windows environment
- host-backed mode is still best-effort shell prototyping, not a live launcher adapter; account tokens, network responses, Java payload contents, and some launcher-specific shell commands remain modeled inputs

## Current limitations

- the Avalonia frontend is now the active PCL-ME UI, but some workflows are still mid-migration
- the new desktop shell now has a portable page-content seam, but many page-specific production contracts are still incomplete
- live launch request execution, real Java transfer networking, and crash save-picker or Explorer behavior still live in the real launcher
- launch-page state is still largely sample-backed even though prompt-command execution is now real inside the shell
- the avalonia can now derive best-effort host-backed inputs, but it still does not source full live launcher state or perform real login/network execution against the current machine

## Troubleshooting

### Fonts are extremely small on `niri` + Wayland

The desktop shell currently relies on Avalonia's default Linux screen-scaling detection:

- [AvaloniaDesktopHost.cs](./Desktop/AvaloniaDesktopHost.cs) only calls `UsePlatformDetect()` and does not override Linux scale handling
- [App.axaml](./Desktop/App.axaml) and the desktop views use many fixed font sizes such as `11`, `12`, `13`, and `14`

That combination is usually fine on macOS because Retina displays report a comfortable logical scale automatically, but on Linux the effective scale can depend on what Avalonia receives from the compositor. Avalonia still has open upstream Linux scaling issues, so on `niri` + Wayland it may end up rendering at scale `1.0`, which makes the fixed font sizes look tiny on HiDPI displays.

Mitigation:

1. Find the output name with `niri msg outputs`. Typical names look like `eDP-1`, `DP-1`, or `HDMI-A-1`.
2. Start the avalonia with an explicit Avalonia scale override:

```bash
AVALONIA_SCREEN_SCALE_FACTORS='eDP-1=1.5' dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

If you use multiple monitors, set each one explicitly:

```bash
AVALONIA_SCREEN_SCALE_FACTORS='eDP-1=1.5;HDMI-A-1=1.0' dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

You can persist the same override in your shell profile or launcher script if you always run the frontend on the same display layout.
