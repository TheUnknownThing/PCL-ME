# I18N Provider Plan

The I18N provider is responsible for loading and providing access to localized strings in the frontend. It subscribes to locale changes in the backend, whose state is managed by `I18nSettingsManager`. The manager is responsible for persisting the user's locale preference.

```text
PCL.Core.Backend/App/I18n/I18nSettingsManager.cs
PCL.Frontend.Avalonia/Workflows/I18nService.cs
```

## API

```csharp
internal interface II18nSettingsManager
{
    string Locale { get; }
    bool SetLocale(string locale);
    event Action<string>? LocaleChanged;
}
```

```csharp
internal interface II18nService
{
    string T(string key);
    string T(string key, IReadOnlyDictionary<string, object?> args);
    bool ReloadCurrentLocale();
    event Action? Changed;
}
```

## Loading

- `I18nService` loads locale files from `PCL.Frontend.Avalonia/Locales`.
- YAML is flattened to `Dictionary<string, string>`.
- Each string is parsed once at load time into cached tokens.
- Token kinds are only `Text` and `Arg`.
- Current locale table is held as a single immutable in-memory snapshot.

## Locale Switch

- `I18nService` subscribes to `I18nSettingsManager.LocaleChanged`.
- On locale switch, load target locale snapshot if not cached.
- Swap the current snapshot atomically.
- Raise `Changed`.

Locale switch should not require software restart. On locale switch, the frontend should automatically perform a refresh when loading is done, and the UI should be refreshed to reflect the new locale.

## Efficiency Rules

- Never parse YAML on `T(...)`.
- Never parse placeholders on `T(...)`.
- `T(...)` only reads the current snapshot and formats tokens.
- Cache snapshots per locale.
