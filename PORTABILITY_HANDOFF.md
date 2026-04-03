# PCL-CE Portability Handoff

## What This Repo Is Now

This repo is no longer in the "prove portability is possible" phase.

Current reality:

- `PCL.Core.Foundation` is a real portable base library.
- `PCL.Core.Backend` is the portable workflow/policy backend.
- `PCL.Core` is increasingly a Windows adapter layer.
- `Plain Craft Launcher 2` is increasingly a WPF shell/adaptation layer.
- `PCL.Frontend.Spike` is the non-WPF proving ground for frontend replacement work.

You can hand this repo to another engineer now.

Do **not** describe it as "frontend migration ready today with nothing left".
Do describe it as "portable backend mostly established, with a finite Windows-shell cleanup list remaining before a real frontend replacement".

## Current Architecture

Use these boundaries as the current source of truth:

- `PCL.Core.Foundation`
  - portable primitives and helpers
  - no WPF
  - no launcher UI assumptions
- `PCL.Core.Backend`
  - portable startup / launch / crash / Java / login workflow and policy
  - the main place new reusable backend logic should go
- `PCL.Core`
  - Windows compatibility layer
  - still contains some Windows-only helpers and legacy glue
- `Plain Craft Launcher 2`
  - current WPF frontend
  - should keep shrinking toward prompts, windowing, UI composition, and OS shell actions
- `PCL.Frontend.Spike`
  - small non-WPF shell prototype
  - use it to prove backend contracts before building a real replacement frontend

## What Has Already Been Extracted

These areas are already substantially moved out of launcher-local policy code:

- startup workflow planning
- startup visual/bootstrap policy
- version transition and milestone policy
- crash export planning and crash response policy
- launch prerun policy
- launch argument/classpath/native/runtime composition
- Microsoft login protocol/request/failure policy
- Authlib login protocol/request/failure policy
- Java runtime selection, transfer planning, and session transition policy
- launcher identity fallback logic in `PCL.Core/App/LauncherIdentity.cs`
- portable encryption-key fallback logic in `PCL.Core/Utils/Secret/EncryptHelper.cs`

The WPF launcher also now has a large shell split instead of keeping everything in giant files.

Important shell modules already in place:

- `Plain Craft Launcher 2/Modules/Base/ModApplicationRuntimeShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowLoadedShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowPresentationShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowShutdownShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowFocusShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowDragShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowWindowShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowChromeShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowPageAnimationShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowPageFrameShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowPageTitleShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowPageSelectionShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowSidebarShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowDragControlShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowNavigationShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowInputShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowPageNameShell.vb`
- `Plain Craft Launcher 2/Modules/Base/ModMainWindowExtraButtonShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchInteractionShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchMicrosoftLoginShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchThirdPartyLoginShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchMicrosoftStepShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchAuthlibStepShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchMicrosoftRequestShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchAuthlibRequestShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchSessionShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunchResultShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModJavaTransferShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModJavaLoaderShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModJavaDownloadSessionShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModJavaSelectionShell.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModCrashPromptShell.vb`

That shell split is real progress. Another engineer should continue from it instead of reopening old extraction debates.

## What Still Blocks A Frontend Migration

These are the remaining tracks that matter before a serious frontend replacement.

### 1. `ModLaunch.vb` is still not just a thin adapter

File:

- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb`

Still needs cleanup around:

- remaining launch-session orchestration
- remaining launcher-side effects
- concrete process/watcher adaptation
- prompt/application glue still mixed with launcher state gathering

This is still one of the biggest blockers.

### 2. `ModJava.vb` still owns concrete download lifecycle behavior

File:

- `Plain Craft Launcher 2/Modules/Minecraft/ModJava.vb`

Still needs cleanup around:

- concrete loader lifecycle
- polling / cancellation / retry behavior
- applying refresh / post-download effects
- final adapter-only shape for Java download execution

### 3. Startup is improved, but not fully adapter-only yet

Files:

- `Plain Craft Launcher 2/Application.xaml.vb`
- `Plain Craft Launcher 2/FormMain.xaml.vb`

`FormMain.xaml.vb` is much smaller in responsibility than before, but it still owns:

- remaining page transition orchestration in `PageChange` / `PageChangeActual`
- some thin event wrappers
- WPF page composition / navigation ownership
- current window/page shell coordination

`Application.xaml.vb` still owns:

- WPF application startup
- splash/runtime presentation
- process-lifetime coordination
- remaining Windows application-shell behavior

These files are no longer the main policy source, but they are still frontend-owned shell code.

### 4. Crash flow still has launcher-owned shell work

File:

- `Plain Craft Launcher 2/Modules/Minecraft/ModCrash.vb`

Still needs cleanup around:

- picker invocation
- direct log opening / reveal behavior
- remaining concrete crash shell actions

### 5. `PCL.Core` still contains Windows-only helpers that block a cleaner backend contract

Examples:

- Windows shell helpers
- clipboard/dialog/process helpers mixed into reusable code paths
- some OS-specific utilities that are still too close to broader services

This does not block all backend work, but it still blocks a cleaner frontend-facing contract.

### 6. `Utils.Secret` is still the biggest architectural portability blocker

Files / area:

- `PCL.Core/Utils/Secret/*`
- profile encryption/decryption call sites such as `Plain Craft Launcher 2/Modules/Minecraft/ModProfile.vb`

Recent progress:

- launcher identity now has a portable fallback path
- encryption key storage now has a portable fallback path

Still not solved:

- a clean, truly headless secret/config/auth boundary
- replacement of the remaining old secret/device-identity assumptions

If someone asks "what is the last serious backend portability blocker?", this is the best answer.

## What A New Engineer Should Do Next

Recommended order:

1. finish `ModLaunch.vb`
2. finish `ModJava.vb`
3. finish `ModCrash.vb`
4. keep shrinking `FormMain.xaml.vb` and `Application.xaml.vb`
5. reduce remaining `PCL.Core` Windows-only helper leakage
6. finish the `Utils.Secret` portability story
7. only then start a real frontend replacement branch

## What Not To Do

Do not:

- move WPF controls/pages into the backend
- reopen already-extracted backend policy work unless there is a bug
- start a brand-new frontend before `ModLaunch.vb`, `ModJava.vb`, and `Utils.Secret` are in better shape
- treat `PCL.Core` as the main home for new portable workflow logic when `PCL.Core.Backend` is the better target

## Frontend Migration Readiness

Current practical status:

- backend policy extraction: strong
- launcher shell thinning: well underway
- actual frontend replacement readiness: not done yet

The migration can be handed off now, but the handoff should be:

"continue shrinking the Windows shell until the replacement frontend only has to own UI and OS adapters"

not:

"build the new frontend immediately and figure the backend out later"

## Validation Commands

Run these before and after major changes:

```bash
dotnet build PCL.Core.Backend/PCL.Core.Backend.csproj -c Debug
dotnet test PCL.Core.Backend.Test/PCL.Core.Backend.Test.csproj -c Debug
dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- startup
dotnet build PCL.Core/PCL.Core.csproj -c Debug
dotnet build "Plain Craft Launcher 2/Plain Craft Launcher 2.vbproj" -c Debug
```

## Short Handoff Message

If you need to hand this to another engineer in one paragraph:

`PCL.Core.Backend` is now the portable source of truth for most startup/launch/crash/login/Java policy, and the WPF launcher is being reduced into shell modules. The remaining blockers before a real frontend migration are mainly `ModLaunch.vb`, `ModJava.vb`, `ModCrash.vb`, the last startup/page shell work in `Application.xaml.vb` and `FormMain.xaml.vb`, and the unfinished `Utils.Secret` portability story. Build on the existing shell split; do not restart the extraction from scratch.`
