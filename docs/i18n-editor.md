# I18N Editor Utility

The repo now includes a standalone developer CLI tool in `PCL.Tools.I18n`.

It is intended for two use cases:

1. CLI-based inspection and validation from the repo root.
2. Programmatic edits and reads inside tooling or tests that reference `PCL.Tools.I18n`.

## What It Understands

The utility treats `PCL.Frontend.Avalonia/Locales` as the source of truth and reads:

- `Meta/manifest.yaml` for the declared locale list.
- `Meta/schema.yaml` for the canonical key set and expected placeholders.
- `<locale>.yaml` for localized string values.

Validation follows the semantics described in the i18n planning docs:

- locale files must use nested mappings with scalar string leaves
- runtime keys are flattened dot paths
- placeholders use named braces like `{name}`
- literal braces use doubled braces like `{{` and `}}`
- every locale must match the schema key set exactly
- every locale value must match the placeholder list declared in schema
- locale files must be declared in `manifest.yaml`

## CLI Usage

Preferred shorthand from the repo root:

```bash
mise run i18n -- validate
mise run i18n -- get --locale en-US --key shell.navigation.pages.launch.title
mise run i18n -- set --locale en-US --key shell.navigation.pages.launch.title --value "Launch"
mise run i18n -- tree --locale en-US --prefix shell.navigation.pages
mise run i18n -- schema tree --prefix shell.navigation
mise run i18n -- schema set --key shell.navigation.pages.launch.title
mise run i18n -- schema set --key shell.navigation.utilities.back_target --placeholders target
mise run i18n -- schema remove --key shell.navigation.legacy.unused
```

Raw `dotnet` fallback:

```bash
dotnet run --project PCL.Tools.I18n/PCL.Tools.I18n.csproj -- validate
```

Supported subcommands:

```text
get --locale <locale> --key <dot.path> [--locales-dir <path>]
set --locale <locale> --key <dot.path> --value <text> [--locales-dir <path>]
tree --locale <locale> [--prefix <dot.path>] [--locales-dir <path>]
validate [--locale <locale>] [--locales-dir <path>] [--format text|msbuild]
schema get --key <dot.path> [--locales-dir <path>]
schema set --key <dot.path> [--placeholders <csv>] [--locales-dir <path>]
schema remove --key <dot.path> [--locales-dir <path>]
schema tree [--prefix <dot.path>] [--locales-dir <path>]
```

Behavior:

- `get` prints the value for a single locale key.
- `set` updates a single locale value in place.
- `tree` prints a nested tree view of one locale, optionally filtered to a subtree with `--prefix`.
- `validate` checks all declared locales, or one locale if `--locale` is provided.
- `validate --format msbuild` emits `MESSAGE|...`, `WARNING|...`, and `ERROR|...` lines for build integration.
- `schema get` prints the placeholder list for a schema key.
- `schema set` creates or updates a schema key. Use `--placeholders a,b,c` for placeholder-bearing strings, or omit `--placeholders` for `[]`.
- `schema set` also fills any missing declared locale entries with the sentinel value `<key>:placeholder`.
- `schema remove` deletes a schema key, prunes empty parent mappings, and removes the same key from declared locale files.
- `schema tree` prints a nested tree view of the schema, optionally filtered to a subtree with `--prefix`.
- all commands return exit code `0` on success and `1` on failure.

If `--locales-dir` is not provided, the command resolves the locale directory in this order:

1. `./PCL.Frontend.Avalonia/Locales`
2. `./Locales`
3. `<app-base>/Locales`

## Programmatic Usage

`I18nYamlEditor` now lives in `PCL.Tools.I18n` and can be referenced directly by other tooling code or tests.

```csharp
using PCL.Tools.I18n;

var editor = new I18nYamlEditor("PCL.Frontend.Avalonia/Locales");

if (editor.TryReadLocaleValue("en-US", "shell.navigation.pages.launch.title", out var title))
{
    Console.WriteLine(title);
}

foreach (var line in editor.RenderSchemaTree("shell.navigation"))
{
    Console.WriteLine(line);
}

editor.SetSchemaValue(
    "shell.navigation.utilities.back_target",
    ["target"]);

editor.SetLocaleValue(
    "en-US",
    "shell.navigation.utilities.back_target",
    "Back to {target}");

var report = editor.Validate();
if (!report.IsValid)
{
    foreach (var issue in report.Issues)
    {
        Console.WriteLine(issue.Message);
    }
}
```

Available entry points:

- `ReadManifestLocales()` returns the declared locale map from `manifest.yaml`.
- `ReadSchema()` returns flattened schema keys and expected placeholder lists.
- `TryReadSchemaValue(key, out placeholders)` reads the placeholder list for a single schema key.
- `SetSchemaValue(key, placeholders)` creates or updates a schema key.
- `RemoveSchemaValue(key)` deletes a schema key, prunes empty parent mappings, and removes that key from declared locale files.
- `RenderSchemaTree(prefix?)` returns a nested display tree for schema keys.
- `ReadLocaleValues(locale)` returns flattened key/value pairs for one locale file.
- `TryReadLocaleValue(locale, key, out value)` reads a single locale value.
- `SetLocaleValue(locale, key, value)` writes a single locale value after schema and placeholder checks.
- `RenderLocaleTree(locale, prefix?)` returns a nested display tree for locale values.
- `Validate(locale?)` validates one locale or the full locale set.

## Write Semantics

`SetLocaleValue(...)` is intentionally strict:

- the locale name must be valid
- the locale must already be declared in `manifest.yaml`
- the key must already exist in `schema.yaml`
- the value's placeholders must match the schema declaration exactly

If the target key is missing from the locale file, the nested mapping path is created automatically.

`SetSchemaValue(...)` is also strict:

- the key must be a valid dot path
- placeholders must be unique, non-empty scalar names
- omitting placeholders writes an empty placeholder list `[]`
- any declared locale file missing that key is automatically backfilled with the sentinel value `<key>:placeholder`

`RemoveSchemaValue(...)` removes the target key, deletes any now-empty ancestor mappings, and removes the same key from declared locale files.

## Build Validation

`PCL.Frontend.Avalonia` runs i18n validation during build by invoking the standalone tool.

Build behavior:

- missing locale keys are errors
- extra locale keys are errors
- placeholder mismatches are errors
- invalid YAML or invalid schema/manifest structure is an error
- locale entries still set to `<key>:placeholder` are warnings

This lets schema changes land without blocking the build immediately, while still surfacing incomplete translations clearly.

## Tree Output

The `tree` and `schema tree` commands print nested key paths with two-space indentation.

Schema example:

```text
shell
  navigation
    utilities
      back []
      back_target [target]
```

Locale example:

```text
shell
  navigation
    utilities
      back = "Back"
      back_target = "Back to {target}"
```

## Validation Output

`Validate(...)` returns `I18nValidationReport`, which exposes:

- `IsValid`
- `Locales`
- `Issues`

Each issue is an `I18nValidationIssue` with:

- `Severity`
- `Code`
- `Message`
- `Locale`
- `Key`
- `FilePath`

Current issue categories include:

- `manifest.invalid`
- `schema.invalid`
- `locale.filename_invalid`
- `locale.undeclared`
- `locale.file_missing`
- `locale.invalid`
- `locale.key_missing`
- `locale.key_extra`
- `locale.placeholder_value`
- `locale.placeholder_mismatch`
