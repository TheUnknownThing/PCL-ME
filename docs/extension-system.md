# Extension system

PCL-ME extensions are .NET 10 modules. They can contribute native Avalonia UI, commands, search providers and background workers through a versioned SDK. Desktop pet and global search use the same contracts as external extensions; the frontend contains only the host integration.

## Using extensions

Open **Settings → Extensions**. The title bar menu and the global search command also navigate to this settings page. Each extension has a standard PCL card with an enable checkbox and expandable settings, shortcuts and diagnostics. Bundled extensions can be enabled or disabled. Global search opens with **Ctrl+Shift+P** on Windows/Linux or **Cmd+Shift+P** on macOS. Search is an in-launcher command palette with category filters, icons, shortcuts and keyboard navigation. It includes commands, launcher pages, settings, installed instances, help and extension providers. Click a result or press Enter to open it; Escape, clicking outside or pressing the shortcut again closes the palette and restores focus.

Install a `.pclext` archive in the manager. New external extensions are disabled until explicitly enabled. Installing a higher version updates an existing external extension. Updates or removals involving an already loaded assembly take effect after restart; the manager displays this state. Bundled extensions update with the launcher and can be disabled, rather than uninstalled. User settings and extension data survive disabling, updates and uninstalling.

To recover from an extension problem, run:

```sh
dotnet run --project src/PCL.Frontend.Avalonia -- app --disable-extensions
```

For a packaged launcher, append `app --disable-extensions` to its executable command. `PCL_DISABLE_EXTENSIONS=1` also starts a session with extensions disabled. Safe mode preserves enablement preferences; you can still change them in the manager for the next normal startup. If the previous process ended during extension activation, that extension is disabled on the next startup and a diagnostic appears in the manager. Corrupt settings are preserved in `.invalid-<timestamp>` files for recovery.

### Bundled extensions

- **Desktop pet** owns its animated window, appearance and interaction settings.
- **Global search** combines launcher and extension search providers.

## Projects and dependencies

The repository groups production assemblies in `src/`, bundled extensions in `extensions/`, tests in `tests/` and developer tools in `tools/`. The solution uses the same folder structure. The public assembly names below are independent of their on-disk location.

| Project | Responsibility |
| --- | --- |
| `PCL.Extensions.Contracts` | UI-independent API, manifest types, launcher snapshots, commands, search, settings, events and workers |
| `PCL.UI` | Shared PCL controls, themes, icons and motion; used by the launcher itself |
| `PCL.Extensions.Avalonia` | Native views, actions, windows and dialogs; depends on Contracts, PCL.UI and Avalonia |
| `PCL.Extensions.Runtime` | Discovery, dependency ordering, lifecycle, isolated assembly loading, package installation, settings and worker protocol |
| `src/PCL.Frontend.Avalonia/Desktop/Extensions` | Launcher workflow adapters, UI placement, shortcuts and extension manager |
| `PCL.Extensions.DesktopPet` | Pet window, drawing, assets, image handling and settings |
| `PCL.Extensions.GlobalSearch` | Native command palette, categories, keyboard navigation and query cancellation |
| `PCL.Tools.Extensions` | Package creation and validation |

Extensions reference Contracts and, for native UI, the Avalonia SDK. They must not reference `PCL.Frontend.Avalonia` or `PCL.Core.Backend`; the loader rejects those dependencies. Host services return public data records rather than internal view models, controls or configuration providers. Each external extension has its own assembly load context for private managed and native dependencies. The SDK, `PCL.UI`, Avalonia and SkiaSharp assemblies are shared with the launcher.

The runtime stores files below `<launcher-data>/Extensions`:

```text
Packages/<id>/          Installed package contents
Data/<id>/settings.json Extension-owned settings
Data/<id>/              Additional extension-owned data
state.json              Host enablement, shortcuts and activation journal
Staging/                Temporary validated installation contents
Pending/<id>/           Updates to apply before discovery on next startup
```

## Build an extension

[`examples/HelloExtension`](../examples/HelloExtension) is a buildable example with a full native page, an instance tab, automatic page menu navigation, an instance context-menu action, a command, settings, localization, a search provider and a worker handler. Its only project dependency is the public Avalonia SDK.

An entry type must be public, have a public parameterless constructor, and implement `IExtension`:

```csharp
public sealed class HelloExtension : IExtension
{
    public ValueTask ActivateAsync(IExtensionContext context, CancellationToken token)
    {
        var ui = context.GetService<IExtensionUi>();
        ui.RegisterView(new()
        {
            Id = "example.hello.page", Title = "Hello",
            Location = ExtensionViewLocation.Page
        }, _ => new PCL.UI.Controls.PclCard
        {
            Header = "Hello", Margin = new Avalonia.Thickness(24),
            CardContent = new TextBlock { Text = "Hello from an extension!" }
        });

        context.Commands.Register(context.Manifest.Commands.Single(),
            cancellation => ui.ShowViewAsync("example.hello.page", cancellationToken: cancellation));
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

Publish the complete dependency output, then pack and validate it:

```sh
dotnet publish examples/HelloExtension -c Release -o artifacts/extensions/hello
dotnet run --project tools/PCL.Tools.Extensions -- pack artifacts/extensions/hello artifacts/extensions/hello.pclext
dotnet run --project tools/PCL.Tools.Extensions -- validate artifacts/extensions/hello.pclext
```

Install the resulting archive in the manager and enable it. `validate` accepts a directory or an archive. Archive entries must be relative paths inside the package; traversal, absolute paths, symlinks, duplicate files, more than 4096 entries and more than 256 MiB of uncompressed data are rejected. Packing does not execute the extension. Validation checks metadata, files, setting defaults and localization references; entry type loading is checked on activation. Tooling allows cross-platform packages; the launcher additionally checks platform and architecture compatibility before installation/discovery.

## Manifest

`extension.json` sits at the archive root. The [JSON schema](extension-manifest.schema.json) supports editor completion. A minimal native package looks like:

```json
{
  "id": "example.hello",
  "name": "Hello extension",
  "version": "1.0.0",
  "apiVersion": 1,
  "avaloniaMajorVersion": 11,
  "entryAssembly": "HelloExtension.dll",
  "entryType": "HelloExtension.HelloExtension",
  "startWithLauncher": true,
  "commands": [
    { "id": "example.hello.open", "title": "Hello" }
  ]
}
```

IDs are lowercase namespaced identifiers, such as `author.feature`. Commands, UI contributions, search providers and published events use the extension ID plus a dot as their prefix. Versions use numeric .NET version syntax, for example `1.2.0`. `dependencies` contains `{ "id": "author.other", "minimumVersion": "1.0.0" }` records. Dependencies must already be installed and enabled; the host activates them first and prevents disabling a dependency while an enabled extension needs it. Cycles and incompatible versions fail activation with diagnostics.

`platforms` optionally restricts the package to `windows`, `linux` and/or `macos`. `architectures` optionally restricts process architectures, such as `x64` or `arm64`. Empty lists allow all. Include the correct native dependencies when distributing packages for several platforms.

## Lifecycle and cleanup

The host serializes activation, enablement, disablement and package changes. `startWithLauncher` activates enabled modules at startup; otherwise manifest commands are available for lazy activation. Explicitly enabling a module also activates it immediately. Every activation receives a new context and instance.

Activation and disposal run on the UI thread. Keep them short and await asynchronous work. Activation has a 10-second deadline; async disposal has a 3-second deadline. Cancellation is cooperative: synchronous native code that blocks the UI thread cannot be interrupted by the host. A timed-out module is marked as requiring restart. Use a worker process for expensive or blocking code.

Command, search, event and UI registrations are automatically owned by the context. Their returned leases can also be disposed early. `context.Own(resource)` adds other `IDisposable` resources. Before disposal, the host cancels `context.Lifetime`; it then calls the extension's `DisposeAsync` and releases registrations in reverse order. Shutdown stops dependents before dependencies. Remove any direct .NET event subscriptions and stop extension-owned tasks in `DisposeAsync`.

`context.RunBackground(async token => ...)` starts lifetime-bound background work and reports unexpected failures. Check the supplied token. Unexpected activation, command and background errors fault the extension and clean up its scope; search-provider and event-listener errors are logged independently so other results/listeners continue working. `ExtensionOperationException` represents an expected host rejection, such as a busy launcher, and does not fault an extension. Diagnostics are visible in the manager.

## Native UI and themes

`IExtensionUi` accepts ordinary Avalonia `Control` and `Window` instances. Extensions can use XAML, bindings, custom controls, drawing, animations, their own assets and scoped styles.

| Contribution | Placement |
| --- | --- |
| `Page` | Full native workspace with Back navigation; automatically discoverable in the extension menu |
| `InstanceTab` | Instance overview tabs; factory receives the selected instance ID |
| `Settings` | Extension manager detail panel |
| `Menu` | Title bar extension menu |
| `Toolbar` | PCL title bar action button; optional vector `IconData` and accessible title |
| `Tray` | Native tray menu |
| `InstanceMenu` | Context menu on instance overview |

Use `RegisterView` factories so the host can recreate views after selection or language changes. Views implementing `IDisposable` are disposed when their host surface is replaced or removed. Menu and toolbar actions invoke a command ID. Instance actions obtain the selected instance through `Launcher.GetInstancesAsync()` (`IsSelected`) or open an instance view with `ShowViewAsync`.

Register contributions and create/show native windows on the UI thread. `ui.Dispatch` posts lifetime-checked callbacks from background work. `ShowViewAsync` dispatches automatically. `ShowWindow` and `ShowDialogAsync` track extension windows and close them on disable. Dialog cancellation closes the dialog and cancels the await. `ShowWithOwner` associates a normal extension window with the launcher; use an unowned window for a desktop companion.

An unowned window that can restore the launcher may set `IsRecoverySurface = true`. The desktop shell then permits the launcher to hide while that surface is available. Closing, minimizing or disabling the last recovery surface restores a hidden launcher when no tray icon is available. Desktop residency is a core shell service and contains no pet-specific behavior.

### Shared controls and UI freedom

`PCL.Extensions.Avalonia` references the public `PCL.UI` assembly. These are the **same controls and themes used throughout the launcher**, extracted into one library rather than copied into an extension-only toolkit. Extensions can use `PclCard`, `PclButton`, `PclIconButton`, `PclIconTextButton`, `PclSearchBox`, `PclListItem`, `PclRadioButton`, `PclTabButton`, `PclSidebarButton` and the other public controls from `PCL.UI.Controls`. `PCL.UI.Icons.PclIcons` provides vector icons; `PCL.UI.Animation` provides the launcher's motion helpers; `PCL.UI.Commands.ActionCommand` adapts actions to command bindings.

```xml
<ui:PclCard xmlns:ui="clr-namespace:PCL.UI.Controls;assembly=PCL.UI"
            Header="My extension">
  <ui:PclCard.CardContent>
    <StackPanel Spacing="12">
      <ui:PclSearchBox Watermark="Find something…" Text="{Binding Query}" />
      <ui:PclButton Text="Run" Command="{Binding RunCommand}" />
    </StackPanel>
  </ui:PclCard.CardContent>
</ui:PclCard>
```

Every view factory accepts an arbitrary native Avalonia `Control`. Developers can compose XAML, custom controls, templates, drawings, animations and their own navigation within a page. Shared controls are optional. The host handles contribution placement, ownership and lifecycle; it does not prescribe a page's internal layout. Keep extension styles scoped to its own views so one extension cannot accidentally restyle the launcher or another extension.

For transient interfaces, `ui.ShowOverlay(content, options)` hosts any native control above the launcher. Options control width, margins, alignment and outside-click/Escape dismissal. The returned `IExtensionOverlay` has `IsOpen`, `Closed` and `Dispose()`. Only one extension overlay is shown at a time. The host cycles keyboard focus inside it, restores the previous focus on dismissal, disposes `IDisposable` content, and closes it when the extension is disabled or a launcher prompt takes priority. Global search uses this API without frontend references. Ordinary pages, settings views, instance tabs, overlays and owned/unowned windows all use the same shared Avalonia environment.

Stable application resources:

| Key | Type |
| --- | --- |
| `Pcl.Foreground`, `Pcl.MutedForeground`, `Pcl.Accent` | Brush |
| `Pcl.Background`, `Pcl.Surface`, `Pcl.SubtleSurface` | Brush |
| `Pcl.Border`, `Pcl.Divider`, `Pcl.Overlay` | Brush |
| `Pcl.OverlayShadow` | BoxShadows |
| `Pcl.Spacing`, `Pcl.CornerRadius` | Double |

Use dynamic resource references for theme changes. Keep custom styles inside the extension's controls/windows and prefix resource keys with the extension namespace. Internal launcher resource names, visual tree structure and private controls are not SDK contracts.

## Commands, search and launcher services

Manifest commands declare titles, optional localization keys, keywords and optional default shortcuts. Activation registers their handlers. `canExecute` runs on the UI thread and should return quickly. The manager permits shortcut overrides and rejects conflicting active command gestures. `Primary` means Cmd on macOS and Ctrl elsewhere. Shortcuts are handled inside the launcher window; they are not OS-global hotkeys.

Host command IDs:

- `pcl.launcher.show`
- `pcl.launcher.launch-selected`
- `pcl.launcher.quit`
- `pcl.extensions.manage`

Search providers receive query text, a result limit, an optional `Kind` filter and a cancellation token. Providers run concurrently on background threads with a two-second deadline per query. Return public `ExtensionSearchResult` records with stable result IDs and either a `CommandId` or a `Route` and arguments. The host assigns `ProviderId`, ranks and deduplicates results, and rejects execution after the provider is removed. Set `Kind` (`Page`, `Setting`, `Instance`, `Command`, `Help` or `Custom`) to place results in the right category; optional vector `IconData` and `Shortcut` customize the presentation. Descriptions should be concise, user-facing text, not route IDs. Providers may ignore the filter when fetching; the host applies it before the global result limit and deduplicates command destinations across providers. Cancel outdated work promptly; the global-search UI also rejects stale query completions and disables old results immediately while a new query runs.

`Launcher.GetInstancesAsync()` returns snapshots of installed instances, including ID, directory, Minecraft version and selection. Treat the ID as opaque. `LaunchAsync(id)` and `NavigateAsync` use the same selection, launch guards and prompts as the launcher UI. `GetLocations()` returns currently localized launcher navigation and help entries. Pass the returned routes back to `NavigateAsync` rather than constructing internal view models. `instance` accepts an `id` argument; `Download/DownloadInstall` accepts a `query` argument for Minecraft version search. `Notify`, `PickFileAsync` and `ShowLauncher` provide common host interactions.

Events carry JSON values:

| Topic | Data |
| --- | --- |
| `launcher.ready` | Empty object; startup activation has completed |
| `launcher.instance.changed` | Selected instance ID string, or null |
| `launcher.locale.changed` | Locale identifier string |

Extensions may publish their own namespaced topics. Subscribe during activation and read current state from the services as well; events do not replay historical values.

## Settings and localization

`context.Settings.Get(key, fallback)` and `Set(key, value)` read/write extension-owned JSON data. Writes use atomic file replacement. `Changed` identifies the changed key; use `ui.Dispatch` when a subscriber updates UI. Arbitrary serializable values are supported, so an extension can maintain richer data beyond the settings exposed in the manager.

Manifest settings generate manager controls for `Boolean`, `String`, `Number`, `Choice` and `File`. `defaultValue` is a string representation; numbers use invariant culture. Numbers can declare minimum/maximum; choices declare value/title pairs; files declare patterns. The extension should use the same defaults when reading its settings. Native `Settings` views can supplement generated controls.

Put flat JSON string dictionaries at `Locales/<locale>.json`. `defaultLocale` defaults to `en-US`. `context.Text.Get(key, fallback)` checks the active language, then the default catalog. `titleKey` localizes commands, views, actions, settings and choices. `extension.name` and `extension.description` localize manager metadata. Handle `launcher.locale.changed` for custom UI text.

Launcher-owned strings continue to use the project's schema-driven YAML i18n system. Extension catalogs belong to their packages and do not add extension-private keys to the launcher schema.

## Worker processes

`context.StartWorker(new("MyWorker.dll", "Example.Worker"))` launches a separate instance of the launcher in worker mode and returns a lifetime-owned `IExtensionWorker`. The public parameterless handler implements `IExtensionWorkerHandler.HandleAsync(method, payload, token)` and `DisposeAsync`. `RequestAsync` exchanges JSON payloads with request IDs, supports up to 32 concurrent requests, caller cancellation and a 30-second deadline. Disabling the extension terminates the worker process tree and completes outstanding requests with cancellation/failure. The protocol uses standard output and routes worker console output to diagnostics via standard error.

Workers have no UI context or implicit launcher service access. Pass snapshots and return results. Messages are bounded to four million characters per line. Worker isolation is for workload and crash separation; a worker has the user's OS privileges.

## Compatibility and trust policy

`apiVersion: 1` is the integration contract. Contracts and the public `PCL.UI` controls/properties remain independent of frontend/backend implementation changes. Internal control template parts and launcher view models are not extension contracts. Preserve existing API signatures, command IDs, documented events, route semantics and resource types within this API version. Add new optional services through `GetService<T>()`; use a new API version for breaking changes and reject unsupported versions explicitly. There are no adapters for the former built-in pet implementation or its settings.

Native extensions additionally declare `avaloniaMajorVersion: 11`. An incompatible major version is rejected before loading. Native Avalonia compatibility is a separate boundary from the UI-independent SDK; extensions using native UI may need rebuilding when that boundary changes. Do not expose extra private launcher APIs to work around it.

Installed modules are trusted native .NET code with access to the user's files, network and process. Separate load contexts and fault reporting do not sandbox native UI or protect against malicious code, unmanaged crashes or a blocked UI thread. The manager explains this before enabling external code. Use extensions from authors you trust, and use safe mode to recover when necessary.

## Validation

`ExtensionRuntimeTest` exercises activation serialization, cleanup, dependency rejection, safe mode, package validation and updates, settings recovery, isolated loading, search cancellation, and real worker requests/cancellation. `FrontendExtensionsTest` uses Avalonia headless integration to verify native pages/tabs/menus/dialogs, language changes, pet lifecycle, hidden-window recovery and global search. `PCL.Frontend.Avalonia.E2E` loads the production application styles, real main window, pane navigation, runtime and bundled extensions with Skia rendering. It drives mouse and keyboard input through settings toggles, editors, global search (categories, keyboard selection, click-to-open, focus restoration, stale results, repeated shortcuts, outside-click and disable cleanup), package installation/update/removal and restart persistence. The platform uses Avalonia headless input with real Skia rendering, including screenshots of light/dark search, empty results, eight locales and the minimum launcher size. It does not automate a native operating-system window server. The operating system file picker is substituted at the platform boundary; archives, isolated loading and settings files are real. Worker process round trips, cancellation and failure recovery are verified by the runtime tests. Screenshots are written to `TestResults/screenshots` under the test output, or to `PCL_E2E_ARTIFACTS` when set. Existing sprite/image and desktop shell tests cover pet rendering and generic recovery behavior.

```sh
dotnet test tests/PCL.Frontend.Avalonia.E2E
dotnet test tests/PCL.Frontend.Avalonia.Test
dotnet test tests/PCL.Core.Backend.Test
dotnet test tests/PCL.Core.Backend.Foundation.Test
dotnet run --project tools/PCL.Tools.I18n -- validate
```
