# Guide for Agents

This project continues the well-known Plain Craft Launcher codebase on Windows, Linux, and macOS. It uses Avalonia as the UI framework and .NET 10 as the runtime.

Agents should strictly follow the architecture and coding style of the existing codebase. The project is organized into multiple layers, with a shared backend assembly (`PCL.Core.Backend`) that contains reusable services and utilities, and a frontend assembly (`PCL.Frontend.Avalonia`) that contains the UI components.

## Native replacements integrity

When modifying `PCL.Frontend.Avalonia/Assets/LauncherAssets/NativeReplacements/native-replacements.json`, make sure every `artifact.sha1` is correct before committing.

You can either:
- verify and edit the SHA1 values manually, or
- run `python3 PCL.Frontend.Avalonia/scripts/recalculate_native_replacements_sha1.py` to recalculate them from `artifact.url`.

When committing code, adhere to conventional commit messages and ensure that your code is well-tested. The project includes unit tests for both the backend and frontend layers, and these should be run before pushing any changes.

## Working on I18N

This project adopts a schema-driven i18n system. When adding new strings, you must ensure that they are properly defined in the schema file as well as in the localization files for each supported language. Read the [i18n documentation](docs/i18n-semantic-guide.md) for more information if you need to work on internationalization. We also provide an [i18n editor](docs/i18n-editor.md) to facilitate the process of managing translations procedurally. Use it to validate or write scriptst to batch update translations when necessary.
