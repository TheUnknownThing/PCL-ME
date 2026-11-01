---
name: develop-extension
description: Develop PCL-ME extensions — bundled extensions under extensions/, external .pclext packages, or the example under examples/. Use when implementing extension features (native views, instance tabs, commands, search providers, workers, settings), writing extension.json manifests, or packing and validating .pclext packages with PCL.Tools.Extensions.
---

# Developing extensions

Read `docs/extension-system.md` first — it is the authoritative guide. This skill is the working summary.

## Project map

| Project | Role |
| --- | --- |
| `src/PCL.Extensions.Contracts` | Public, UI-independent API: `IExtension`, manifest records, commands, search, settings, events, workers. |
| `src/PCL.Extensions.Avalonia` | Native UI contracts (`IExtensionUi`): views, actions, windows, dialogs. |
| `src/PCL.Extensions.Runtime` | Host-side loader: discovery, dependency ordering, isolated assembly-load contexts, `.pclext` installation, per-extension settings and worker protocol. |
| `extensions/PCL.Extensions.DesktopPet`, `extensions/PCL.Extensions.GlobalSearch` | Bundled extensions — reference implementations using the same contracts as external ones. |
| `examples/HelloExtension` | Buildable sample covering a page, instance tab, context-menu action, command, settings, localization, search provider, and worker. |
| `tools/PCL.Tools.Extensions` | `pack` / `validate` CLI for `.pclext` archives. |

## Hard rules

- Extensions reference only `PCL.Extensions.Contracts` (plus `PCL.Extensions.Avalonia` for native UI). They must **not** reference `PCL.Frontend.Avalonia` or `PCL.Core.Backend` — the loader rejects such dependencies.
- Host services hand out public data records, never internal view models, controls, or config providers. Use the context services (`context.GetService<...>()`, `Launcher` snapshots, `context.Commands`, `context.Search`, `context.Events`) instead of reaching into launcher internals.
- Entry type: public, parameterless constructor, implements `IExtension`. Activation and `DisposeAsync` run on the UI thread — keep them short (10 s activation deadline, 3 s disposal). Offload blocking work to `context.RunBackground(async token => ...)` or a worker process.
- Localization lives in the extension's own `Locales/*.json` files, referenced from the manifest via `titleKey` etc. — not in the launcher's `Locales/`.

## Building blocks

`extension.json` (schema: `docs/extension-manifest.schema.json`) declares `id` (lowercase, namespaced like `author.feature`), `version`, `apiVersion`, `entryAssembly`, `entryType`, optional `commands`, `settings`, `dependencies`, `platforms`, `architectures`.

Native UI contributions via `IExtensionUi.RegisterView` with `ExtensionViewLocation`: `Page`, `InstanceTab`, `Settings`, `Menu`, `Toolbar`, `Tray`, `InstanceMenu`. Use factory lambdas (views are recreated on selection/language changes) and the shared `PCL.UI` controls — the same `PclCard`/`PclButton`/`PclSearchBox`/`PclIcons` the launcher itself uses.

Manifest settings (`"settings": [{ "key", "title", "kind", "defaultValue" }]`) surface in the extension manager; read them through `context.Settings`.

## Build, pack, validate, test

```sh
dotnet publish examples/HelloExtension -c Release -o artifacts/extensions/hello
dotnet run --project tools/PCL.Tools.Extensions -- pack artifacts/extensions/hello artifacts/extensions/hello.pclext
dotnet run --project tools/PCL.Tools.Extensions -- validate artifacts/extensions/hello.pclext
```

Install the `.pclext` in **Settings → Extensions**; newly installed external extensions start disabled. Updates/removals of already-loaded assemblies apply after restart (the manager shows this state).

Test coverage:

```sh
dotnet test tests/PCL.Frontend.Avalonia.Test/PCL.Frontend.Avalonia.Test.csproj -c Debug    # ExtensionRuntimeTest, FrontendExtensionsTest, …
dotnet test tests/PCL.Frontend.Avalonia.E2E/PCL.Frontend.Avalonia.E2E.csproj -c Debug     # ExtensionManagementE2E, GlobalSearchE2E
```

For bundled extensions (`extensions/`), build and run the launcher normally — they ship with it and are referenceable from the frontend project; add or adapt unit tests next to the existing extension tests.

## Debugging

```sh
dotnet run --project src/PCL.Frontend.Avalonia -- app --disable-extensions   # safe mode; keeps enablement preferences
PCL_DISABLE_EXTENSIONS=1 …                                                   # environment-variable equivalent
```

Extension state lives under `<launcher-data>/Extensions/` (`Packages/`, `Data/<id>/settings.json`, `state.json`, `Staging/`, `Pending/`). A crashed activation auto-disables the extension on next start and surfaces a diagnostic in the manager; corrupt settings are preserved as `.invalid-<timestamp>` files.
