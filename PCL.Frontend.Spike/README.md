# PCL.Frontend.Spike

`PCL.Frontend.Spike` is the current replacement-shell prototype for the extracted startup / launch / crash workflow layer.

It targets plain `net8.0`, references `PCL.Core.Backend`, and intentionally avoids WPF or Windows-only frontend code.

## What it proves

- a non-WPF shell can consume the extracted startup, launch, and crash contracts
- the portable backend can drive both machine-readable output and shell-oriented transcript output
- the same prototype shell can materialize bootstrap, prerun, and crash-export artifacts in a real workspace through adapter-style file execution
- the shell can round-trip startup, launch, and crash inputs through `_inputs/*.json` snapshots and replay them later with `--input-root`
- frontend concerns can be modeled as prompt decisions, file-work summaries, and process or shell transcripts without pulling workflow policy back into the launcher

## Commands

Default behavior stays compatibility-friendly:

- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- startup`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- launch legacy-forge`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- crash`

These emit `plan`-mode JSON by default.

Useful reviewer commands:

- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- startup --mode run --format text`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- launch legacy-forge --mode run --format text --java-prompt download`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- launch legacy-forge --mode run --format text --java-prompt abort`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- crash --mode run --format text --crash-action export`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- all legacy-forge --mode run --format text`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- launch legacy-forge --mode execute --format text --workspace /tmp/pcl-launch-spike`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- crash --mode execute --format text --workspace /tmp/pcl-crash-spike`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- all legacy-forge --mode execute --format text --workspace /tmp/pcl-shell-spike`
- `dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- launch modern-fabric --mode run --format text --input-root /tmp/pcl-launch-spike/_inputs/launch.json`

## Options

- `--mode plan|run|execute`
- `--format json|text`
- `--scenario modern-fabric|legacy-forge`
- `--java-prompt download|abort`
- `--crash-action close|view-log|open-settings|export`
- `--workspace /absolute/or/relative/path`
- `--input-root /path/to/file/or/_inputs_directory`

`execute` mode creates a workspace root automatically when `--workspace` is omitted.

## Execute mode outputs

- startup execution creates bootstrap directories, deletes seeded legacy log placeholders, and writes prompt/config artifacts
- launch execution can materialize `launcher_profiles.json`, `options.txt`, a generated launch batch file, and session summary artifacts
- crash execution can stage sample input files and build a real crash zip archive via `MinecraftCrashExportArchiveService`
- aborting the launch Java prompt in `execute` mode stops before prerun file work, so no launch artifacts are created
- each execution workspace also stores `_inputs/*.json` snapshots so the same shell inputs can be replayed later

## Input replay

- `--input-root` accepts either a single JSON file such as `launch.json` or a directory containing `startup.json`, `launch.json`, or `crash.json`
- when `--input-root` is provided, the spike reuses those serialized inputs instead of the built-in defaults
- this makes it easy to execute once, tweak the saved JSON, and rerun the shell against file-backed state

## Current limitations

- the spike is still a prototype shell, not a user-facing replacement launcher
- launch request execution, Java download plumbing, and crash save-picker or Explorer behavior still live in the real launcher
- the spike still starts from deterministic defaults unless you point it at saved or edited input JSON, and it does not yet source those inputs from a live launcher environment
