# Frontend Migration Plan

## Goal

Replace the WPF frontend only after the remaining launcher shell is thin enough.

The intended end state is:

- `PCL.Core.Backend` owns portable workflow/policy
- `PCL.Core` owns Windows compatibility helpers only
- the current WPF project owns legacy UI until replacement
- the future frontend owns UI, prompt rendering, and OS shell actions only

## Current Status

Good news:

- the backend side is already far enough along to hand to another engineer
- `PCL.Frontend.Spike` exists and proves non-WPF consumption of the backend
- `FormMain.xaml.vb` has already been heavily split into shell modules
- login/Java/crash/startup policy is mostly no longer trapped in giant WPF files

Important caveat:

- this is **not** yet the moment to start a full frontend rewrite
- there is still a meaningful Windows-shell cleanup list first

## Done Already

The following are already in a usable migration state:

- portable foundation layer in `PCL.Core.Foundation`
- portable workflow backend in `PCL.Core.Backend`
- portable backend tests in `PCL.Core.Backend.Test`
- shell prototype in `PCL.Frontend.Spike`
- startup shell extraction
- Microsoft/Authlib login shell extraction
- Java transfer/session shell extraction
- crash prompt shell extraction
- large `FormMain.xaml.vb` shell split
- launcher identity fallback in `PCL.Core/App/LauncherIdentity.cs`
- encryption key fallback in `PCL.Core/Utils/Secret/EncryptHelper.cs`

## Remaining Work Before A Real Frontend Migration

### Priority 1: finish launch and Java adapter cleanup

Files:

- `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb`
- `Plain Craft Launcher 2/Modules/Minecraft/ModJava.vb`

Needed outcome:

- these become adapter-first shells
- they stop being mixed policy/orchestration hubs
- backend services remain the source of truth

### Priority 2: finish crash and startup shell cleanup

Files:

- `Plain Craft Launcher 2/Modules/Minecraft/ModCrash.vb`
- `Plain Craft Launcher 2/Application.xaml.vb`
- `Plain Craft Launcher 2/FormMain.xaml.vb`

Needed outcome:

- remaining behavior is mostly prompt rendering, page routing, windowing, and OS actions
- no important launch/startup/crash policy is trapped in WPF files

### Priority 3: finish the portability story for secrets

Area:

- `PCL.Core/Utils/Secret/*`
- encrypted profile/config call sites

Needed outcome:

- a real headless auth/config boundary
- no hidden dependency on Windows-only secret/device identity assumptions

This is the biggest backend-side blocker left.

### Priority 4: reduce leftover Windows leakage in `PCL.Core`

Needed outcome:

- reusable services stop depending on Windows-only helpers by accident
- backend contracts become easier for a new frontend to consume

## What A Replacement Frontend Should Not Have To Rebuild

When the frontend migration actually starts, the new frontend should **not** need to recreate:

- launch policy
- login protocol logic
- Java selection/download policy
- crash export planning
- startup workflow planning
- version-transition policy
- milestone/update-log policy

If any of those are still frontend-owned at migration time, the cleanup is not done yet.

## Practical Readiness Checklist

A new frontend migration branch should start only when these statements are true:

- `ModLaunch.vb` is mostly shell-only
- `ModJava.vb` is mostly shell-only
- `ModCrash.vb` is mostly shell-only
- `Application.xaml.vb` and `FormMain.xaml.vb` are mostly UI/window shells
- `Utils.Secret` no longer blocks headless backend consumption
- `PCL.Frontend.Spike` can exercise the key workflows without borrowing WPF behavior

## Suggested Execution Order

1. finish `ModLaunch.vb`
2. finish `ModJava.vb`
3. finish `ModCrash.vb`
4. keep shrinking `Application.xaml.vb`
5. keep shrinking `FormMain.xaml.vb`
6. finish `Utils.Secret`
7. tighten remaining Windows-only helpers in `PCL.Core`
8. expand `PCL.Frontend.Spike` only as needed to prove those seams
9. create the real replacement frontend only after the above list is materially done

## Validation

Use this as the standard validation loop:

```bash
dotnet build PCL.Core.Backend/PCL.Core.Backend.csproj -c Debug
dotnet test PCL.Core.Backend.Test/PCL.Core.Backend.Test.csproj -c Debug
dotnet run --project PCL.Frontend.Spike/PCL.Frontend.Spike.csproj -- startup
dotnet build PCL.Core/PCL.Core.csproj -c Debug
dotnet build "Plain Craft Launcher 2/Plain Craft Launcher 2.vbproj" -c Debug
```

## Short Advice For The Next Engineer

Do not treat this as a greenfield frontend project.

Treat it as:

1. finish shrinking the Windows launcher into adapters
2. keep proving backend contracts with `PCL.Frontend.Spike`
3. only then build the new frontend
