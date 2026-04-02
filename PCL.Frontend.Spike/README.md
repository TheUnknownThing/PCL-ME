# PCL.Frontend.Spike

`PCL.Frontend.Spike` is the current replacement-shell prototype for the extracted startup / launch / crash workflow layer.

It targets plain `net8.0`, references `PCL.Core.Backend`, and intentionally avoids WPF or Windows-only frontend code.

## What it proves

- a non-WPF shell can consume the extracted startup, launch, and crash contracts
- the portable backend can drive both machine-readable output and shell-oriented transcript output
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

## Options

- `--mode plan|run`
- `--format json|text`
- `--scenario modern-fabric|legacy-forge`
- `--java-prompt download|abort`
- `--crash-action close|view-log|open-settings|export`

## Current limitations

- the spike is still a prototype shell, not a user-facing replacement launcher
- launch request execution, Java download plumbing, and crash save-picker or Explorer behavior still live in the real launcher
- the spike currently exercises deterministic sample inputs rather than a full live runtime environment
