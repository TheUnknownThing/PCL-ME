# I18N Semantic Guide

## File Position

Translation files live under:

```text
PCL.Frontend.Avalonia/Locales/<locale-name>.yaml
```

This translates to the following directory structure:

```text
PCL.Frontend.Avalonia/
  Locales/
    Meta/
      schema.yaml
      manifest.yaml
    en-US.yaml
    zh-Hans.yaml
    zh-Hant.yaml
```

YAML is authored as nested objects, but runtime lookup uses flattened dot-path keys.

Example:

```yaml
common:
  actions:
    exit: "Exit"
```

This defines key:

```text
common.actions.exit
```

Which is used in the frontend to resolve the string "Exit" for the current locale. Only the frontend has access to translation files.

## Meta Directory

The `Meta` directory contains metadata files used for indexing and validation of translation files.

### schema.yaml

This file defines the expected structure of translation files.

```
common:
  actions:
    exit: []
  info:
    game_version:
      - version
```

Each key maps to an array of expected placeholders. If the array is empty, the string is expected to have no placeholders.

When compiling a release, the build process will validate that all translation files conform to the schema. Any additional, missing keys or mismatched placeholders will cause a build failure.

### manifest.yaml

This file defines maps available locales to their display names.

```yaml
locales:
  en-US: "English"
  zh-Hans: "简体中文"
  zh-Hant: "繁體中文"
```

## Semantic Definition

Each translation string can contain any number of placeholders. We shall use named braces for placeholders, and double braces for literal braces. Unnamed braces are explicitly disallowed.

Example:

```text
"Hello, {name}! Today is {{ {day} }}."
```
