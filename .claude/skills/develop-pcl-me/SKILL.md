---
name: develop-pcl-me
description: Develop the PCL-ME launcher itself — build, run, and test the backend services, Avalonia desktop frontend, or shared PCL.UI library, and follow repository conventions for i18n, native assets, and commits. Use for any change to launcher features, UI, workflows, packaging, or release engineering that is not an extension.
---

# Developing PCL-ME

PCL-ME (Plain Craft Launcher, Multiplatform Edition) is an Avalonia desktop launcher for Minecraft running on .NET 10, with a split backend/frontend architecture plus an extension system.

## Where code goes

| Directory | Contents |
| --- | --- |
| `src/PCL.Core.Backend` | Launcher services, Minecraft launch workflows, downloads, Java management, platform utilities. Must stay UI-free. |
| `src/PCL.Frontend.Avalonia` | Desktop app: shell (`Desktop/`), pages as panes (`Desktop/Panes/`), view models (`ViewModels/LauncherViewModel.*.cs` partials), workflows (`Workflows/`), CLI (`Cli/`), assets and packaging (`Assets/`, `scripts/`). Also hosts the extension session (`Desktop/Extensions/`). |
| `src/PCL.UI` | Shared controls (`PclCard`, `PclButton`, `PclSearchBox`, …), themes, `PclIcons`, and motion helpers. Used by both the frontend and extensions — keep it free of launcher-specific logic. |
| `src/PCL.Extensions.*` | Extension SDK — see the `develop-extension` skill. |
| `tests/` | `PCL.Core.Backend.Foundation.Test`, `PCL.Core.Backend.Test`, `PCL.Frontend.Avalonia.Test` (unit/integration) and `PCL.Frontend.Avalonia.E2E` (headless desktop E2E driving the real app). |
| `tools/PCL.Tools.I18n` | i18n editor/validator CLI. |

Conventions:

- View models are partial classes split by concern (`LauncherViewModel.SetupComposition.cs`, …); workflows live in `Workflows/` as composable services. Match the existing naming (`Frontend*Service`, `Launcher*Service`) and file placement instead of inventing new top-level folders.
- UI uses the shared `PCL.UI` controls and `PclIcons`/`PclTheme` — do not reintroduce frontend-private copies of moved controls.
- Backend code never references Avalonia or the frontend.

## Build, run, test

```sh
dotnet build src/PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -c Debug   # builds the full dependency graph
dotnet run --project src/PCL.Frontend.Avalonia -- app                          # run the launcher
dotnet test tests/PCL.Core.Backend.Foundation.Test/PCL.Core.Backend.Foundation.Test.csproj -c Debug
dotnet test tests/PCL.Core.Backend.Test/PCL.Core.Backend.Test.csproj -c Debug
dotnet test tests/PCL.Frontend.Avalonia.Test/PCL.Frontend.Avalonia.Test.csproj -c Debug
dotnet test tests/PCL.Frontend.Avalonia.E2E/PCL.Frontend.Avalonia.E2E.csproj -c Debug
```

`mise run launch | build | test` mirrors the CI workflow (`mise run test` = frontend build + the two backend test projects). CI (`.github/workflows/build-test.yml`) runs the build and the three unit/integration projects; E2E is currently local-only. Run the tests affected by your change before pushing.

Packaging (release) uses `src/PCL.Frontend.Avalonia/scripts/package-frontend.sh <rid>` — see `src/PCL.Frontend.Avalonia/README.md`.

## E2E notes

E2E tests drive the real shell headlessly (`DesktopFixture`): they click tabs, type into search boxes, and assert on realized visual trees. Two gotchas:

- `Find<T>(name)` walks the **visual** tree. Content inside a `PclCard.CardContent` (or any `ContentPresenter`) only exists once the container has been measured — re-resolve named elements after expanding/showing their host instead of capturing them while hidden.
- Prefer `Wait(...)`/`Settle()` helpers over fixed delays; screenshots (`app.Screenshot(name)`) land in the test-results directory for inspection.

## i18n

Strings are schema-driven: every user-visible string must exist in `src/PCL.Frontend.Avalonia/Locales/Meta/schema.yaml` **and** in every `Locales/*.yaml` locale file, keyed semantically (e.g. `shell.navigation.pages.launch.title`). The frontend build fails on validation errors. Batch-edit via the editor tool:

```sh
dotnet run -p:NuGetAudit=false --project tools/PCL.Tools.I18n/PCL.Tools.I18n.csproj -- validate
mise run i18n -- <subcommand>   # same tool through mise
```

See `docs/i18n-semantic-guide.md` and `docs/i18n-editor.md`.

## Native replacements

`src/PCL.Frontend.Avalonia/Assets/LauncherAssets/NativeReplacements/native-replacements.json` lists downloaded native binaries with `artifact.sha1` checksums. After editing it, recalculate checksums with `python3 src/PCL.Frontend.Avalonia/scripts/recalculate_native_replacements_sha1.py` (or verify by hand) — never commit a stale SHA1.

## Commits

Conventional commits (`feat(ui): …`, `fix(minecraft): …`, `refactor(repo): …`, `test(frontend): …`, `docs: …`). Keep each commit to one concern; run the relevant tests first. New projects belong in `src/`, `tests/`, `tools/`, `extensions/`, or `examples/` and must be registered in `Plain Craft Launcher 2.slnx` under the matching folder.
