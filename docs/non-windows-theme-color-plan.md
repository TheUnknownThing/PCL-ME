## Non-Windows Theme Color Switch Plan

Scope:
- Add runtime theme application for Avalonia appearance settings.
- Enable theme color switching only for non-Windows builds.
- Keep the restriction controlled at compile time, with a runtime guard as defense in depth.

Constraints:
- `UiDarkMode`, `UiLightColor`, and `UiDarkColor` already exist in persisted settings.
- The current app startup path does not apply those settings to Avalonia resources.
- A large number of AXAML and C# files still contain hardcoded colors, so migration must be staged.

Implementation stages:
1. Add a compile-time feature symbol for non-Windows builds.
   - Default theme color switching to disabled.
   - Enable it only when the build target is explicitly non-Windows, or when no target RID is supplied and the build host is not Windows.

2. Add a central appearance service.
   - Read `UiDarkMode`, `UiLightColor`, and `UiDarkColor`.
   - Apply `RequestedThemeVariant`.
   - Apply a palette by overriding Avalonia resource keys at application scope.
   - Keep dark mode available on all builds.
   - Keep theme color switching compiled out on Windows builds.

3. Wire startup and live updates.
   - Apply appearance during `App.Initialize()`.
   - Reapply appearance when setup values change.
   - Reapply after setup composition reloads and reset actions.

4. Hide the theme color UI on Windows builds.
   - Keep the dark mode selector visible.
   - Hide color selectors and show a notice that the current build does not support theme color switching.
   - Gate the visibility and normalization logic with the compile-time feature symbol.

5. Migrate high-impact hardcodes first.
   - Update shared controls that represent the launcher accent:
     - `PclButton`
     - `PclLaunchButton`
     - `PclOutlineButton`
     - `PclRadioButton`
   - Keep semantic colors such as error/success as separate resources or existing semantic values.

6. Follow-up migration pass.
   - Move additional accent-related literals from setup views, shell views, and view models onto palette resources.
   - Leave non-accent content colors alone unless they block theme correctness.

Verification:
- Non-Windows build:
  - startup applies dark mode and selected palette
  - setup page updates appearance live
  - reset UI settings reapplies defaults
- Windows build:
  - theme mode still works
  - theme color selectors are not usable
  - palette switching code path is compiled out
