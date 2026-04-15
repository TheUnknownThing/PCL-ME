# I18N Progress Tracker

## Done

- [x] Backend-frontend transport migration: replace backend-owned UI strings with enums, content keys, and raw-text exception paths.
- [x] Shell navigation and page-content localization: route titles, summaries, breadcrumbs, sidebar groups, utility labels, and prompt lane chrome.
- [x] Shared shell: overlay dialogs, common buttons, activity/failure text, loading and empty states.
- [x] Launch: homepage cards, launch dialog, account/profile actions, launch status copy.
- [x] Download / Auto Install: presets, selectors, step labels, progress and error text.
- [x] Download / Resource Catalog: search, filters, loading/empty states, install actions.
- [x] Download / Favorites: favorite actions, batch install flow, share/import dialogs, summaries.
- [x] Resource Detail: metadata cards, dependency/install actions, warnings, fallback text.
- [x] Setup / Launch: option labels, descriptions, reset/refresh feedback.
- [x] Setup / UI: appearance, background, visibility, animation, preview copy.
- [x] Setup / Game Manage: instance/file management settings, confirmations, hints.
- [x] Setup / About: version info, project links, diagnostics copy.
- [x] Setup / Log: log settings, switches, retention and diagnostic hints.
- [x] Setup / Feedback: feedback channels, submission hints, failure text.
- [x] Setup / Update: update channel/status text, actions, restart/install prompts.
- [x] Setup / Java: Java discovery, picker, compatibility hints, repair actions.
- [x] Setup / Launcher Misc: remaining launcher toggles and descriptions.
- [x] Tools / Test: test controls, placeholders, result text.
- [x] Tools / Help: help list chrome, search/empty states, quick actions.
- [x] Help Detail: article chrome, related actions, fallback and error text.
- [x] Instance Select: list actions, filters, empty states, repair/import/delete dialogs.
- [x] Task Manager: task titles, status wrappers, progress summaries, cancel failure wrappers.
- [x] Game Log: log page chrome, crash export actions, session status text.
- [x] Instance / Overview: overview cards, quick actions, state summaries.
- [x] Instance / Settings: instance settings, inheritance toggles, validation text.
- [x] Instance / Install: component chooser, version list states, apply summaries.
- [x] Instance / Export: export options, config read/write flow, guide text.
- [x] Instance / World & Screenshot: file actions, empty states, open/delete confirmations.
- [x] Instance / Resources: Mod, disabled Mod, resource pack, shader, schematic surfaces.
- [x] Instance / Server: server list actions, validation, connect/open prompts.
- [x] Save Detail / Overview: save metadata, quick actions, empty/error states.
- [x] Save Detail / Backup: backup list, restore/delete confirmations, progress text.
- [x] Save Detail / Datapack: datapack actions, import/remove confirmations, status text.

## TODO

These still require localization work.

- [x] 跨平台版提示 and the content below
- [x] 搜索 (Search) buttons should be localized, sharing one common key
- [x] Various loading screens, like 正在获取xxx
- [x] Tags should be localized (or query for tags respect localization)
- [x] Number of downloads should be localized, with the number respecting locale standards (EN: K/M/B, CN: 万/亿, etc.)
- [x] In mod installation screen, the mod description should be localized.
- [x] Memory layout display should be localized.
- [x] Tools > Help article loading should resolve locale-specific assets with English fallback instead of classic i18n keys.
- [ ] 已启动 xx 次 in Overview tab
- [ ] Mod list 已安装 未安装 安装器, along with missing translation key for instance.install.option.loading_versions
- [ ] Instance Settings > Worlds: 创建时间 最后修改时间
- [ ] Instance Settings > Servers: Missing text in all buttons
- [ ] Content in Tools > Help. Initial English article seeds are in place, but broader localized coverage is still needed for the remaining help topics.
