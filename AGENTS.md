# Guide for Agents

This project continues the well-known Plain Craft Launcher codebase on Windows, Linux, and macOS. It uses Avalonia as the UI framework and .NET 10 as the runtime.

Agents should strictly follow the architecture and coding style of the existing codebase.

Popup dialogs must be hosted as overlays inside the launcher window. Never create a separate window for a popup: separate popup windows break tiling window managers. Keep popup menus and dropdowns in the window's overlay layer as well.

## Repository layout

| Directory | Contents |
| --- | --- |
| `src/` | Production assemblies. |
| `extensions/` | Extensions bundled with the launcher, one project per extension. |
| `tests/` | Unit, integration, and desktop end-to-end tests. |
| `tools/` | Developer tools: the i18n editor and the extension packager. |
| `examples/` | Buildable samples for extension developers. |
| `docs/` | Architecture and workflow documentation. |

Production assemblies under `src/`:

| Project | Responsibility |
| --- | --- |
| `PCL.Core.Backend` | Shared launcher services, Minecraft launch workflows, downloads, platform utilities. No UI dependencies. |
| `PCL.Frontend.Avalonia` | Desktop UI, workflows, application packaging, and the extension host integration. |
| `PCL.UI` | Shared controls, themes, icons, and motion used by both the launcher and extensions. |
| `PCL.Extensions.Contracts` | Public, UI-independent extension API. |
| `PCL.Extensions.Runtime` | Extension discovery, lifecycle, isolated assembly loading, and package installation. |
| `PCL.Extensions.Avalonia` | Native Avalonia UI contribution contracts for extensions. |

## Skills

Two skills in `.claude/skills/` cover the main workflows — prefer them over rediscovering the conventions:

- `develop-pcl-me` — building, running, testing, and changing the launcher itself.
- `develop-extension` — building extensions on the extension SDK, including bundled extensions under `extensions/`.

## Build and test

```sh
dotnet build src/PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -c Debug   # also builds all dependencies
dotnet test tests/PCL.Core.Backend.Foundation.Test/PCL.Core.Backend.Foundation.Test.csproj -c Debug
dotnet test tests/PCL.Core.Backend.Test/PCL.Core.Backend.Test.csproj -c Debug
dotnet test tests/PCL.Frontend.Avalonia.Test/PCL.Frontend.Avalonia.Test.csproj -c Debug
dotnet test tests/PCL.Frontend.Avalonia.E2E/PCL.Frontend.Avalonia.E2E.csproj -c Debug   # desktop E2E, not part of CI
dotnet run --project src/PCL.Frontend.Avalonia -- app   # run the launcher
```

`mise` provides the same steps as `mise run launch | build | test`. When committing code, adhere to conventional commit messages and ensure that your code is well-tested; run the relevant test projects before pushing any changes.

## Native replacements integrity

When modifying `src/PCL.Frontend.Avalonia/Assets/LauncherAssets/NativeReplacements/native-replacements.json`, make sure every `artifact.sha1` is correct before committing.

You can either:
- verify and edit the SHA1 values manually, or
- run `python3 src/PCL.Frontend.Avalonia/scripts/recalculate_native_replacements_sha1.py` to recalculate them from `artifact.url`.

## Working on I18N

This project adopts a schema-driven i18n system. When adding new strings, you must ensure that they are properly defined in the schema file as well as in the localization files for each supported language. Read the [i18n documentation](docs/i18n-semantic-guide.md) for more information if you need to work on internationalization. We also provide an [i18n editor](docs/i18n-editor.md) to facilitate the process of managing translations procedurally. Use it to validate or write scripts to batch update translations when necessary. The frontend build runs this validation automatically and fails on errors.

Extensions localize through their own `Locales/*.json` files; see the [extension system documentation](docs/extension-system.md) for that workflow.
