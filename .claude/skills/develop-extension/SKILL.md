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

Native views use `IExtensionUi.RegisterView` with `ExtensionViewLocation.Page`, `InstanceTab` or `Settings`. Command-based actions use `RegisterAction` with `ExtensionActionLocation.Menu`, `Toolbar`, `Tray` or `InstanceMenu`. Use factory lambdas (views are recreated on selection/language changes) and the shared `PCL.UI` controls — the same `PclCard`/`PclButton`/`PclSearchBox`/`PclIcons` the launcher itself uses.

Manifest settings (`"settings": [{ "key", "title", "kind", "defaultValue" }]`) surface in the extension manager; read them through `context.Settings`.

## SDK 1.1 capabilities

Keep the original SDK interfaces and assembly identities compatible. New functionality is exposed through optional services from `GetService<T>()`; declare `minimumSdkVersion: "1.1.0"` and required/optional capability IDs in the manifest. Check `IExtensionCapabilities.Supports` for optional integration. Add defaulted DTO properties; do not change old positional constructors or add abstract members to published interfaces.

- `IExtensionContributions`: Download/Settings/Tools pages (`RegisterPage`/`ShowPageAsync`), cards in `ExtensionRegions`, and captured Instance/Mod/World/Resource actions. Pages enter launcher locations/global search. Factories receive instance/version/loader/world/query snapshots and a mount token. Use that token, keep factories short, and implement `IDisposable` for view cleanup. Unrelated registrations preserve editors.
- `IExtensionInstances`: detailed resource provenance, loader/game-directory snapshots, staged create/clone, recoverable removal and refresh. Use public records; all file and install workflows live under frontend `Workflows/` behind host adapters.
- `IExtensionResourceProviders`/`IExtensionResources`: typed, cancellable catalogs and releases; prepare a dependency-resolved preview, display destinations/replacements, then apply its host-owned one-use ID. Provider ordering selects the first compatible release. Handle unidentified files explicitly when migrating. Explicit-provider pagination supports limits 1–100; federated queries are first-page only.
- `IExtensionTasks`: managed progress, failure and cancellation through the real task manager. Callbacks run off the UI thread; capture control values before entering them. IDs are namespaced and unique while running.
- `ExtensionTopics`: observe discovery/content/process changes. `InstanceInstalled` is the completed-install barrier, with `IsNewInstance`; filesystem discovery events can arrive earlier and may overlap/coalesce. UI updates require `ui.Dispatch`.
- `IExtensionLaunchHooks`: ordered, bounded async preparation with explicit stop/continue policy, validated JVM/environment additions and no authentication snapshot. Observe cancellation; use a worker for blocking native work.

`examples/WorkflowExtension` demonstrates private JSON catalogs, skins, post-install mod presets, instance tools, world backups, preference recording and a launch hook using only public assemblies. Its E2E tests install it as an external `.pclext`. `ExtensionCapabilitiesE2E` verifies real workflows and rollback, not just activation; frozen API 1 fixtures check binary compatibility. Never update the frozen reference sources to match current SDK changes.

## Build, pack, validate, test

```sh
dotnet publish examples/HelloExtension -c Release -o artifacts/extensions/hello
dotnet run --project tools/PCL.Tools.Extensions -- pack artifacts/extensions/hello artifacts/extensions/hello.pclext
dotnet run --project tools/PCL.Tools.Extensions -- validate artifacts/extensions/hello.pclext
```

Install the `.pclext` in **Tools → Extensions**; newly installed external extensions start disabled. Updates/removals of already-loaded assemblies apply after restart (the manager shows this state).

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
