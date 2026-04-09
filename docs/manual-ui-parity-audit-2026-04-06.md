# Manual UI Parity Audit

Date: 2026-04-06

## Scope

Manual side-by-side comparison between:

- left: gold implementation
- right: current cross-platform implementation in `PCL.Frontend.Avalonia`

Test method:

1. Open both launcher windows side by side.
2. Click the same top-level routes and selected subpages on both sides.
3. Record visible parity gaps that affect structure, controls, wording, or overall UX.

This pass covered the following surfaces:

- Launch home
- Launch -> instance selection
- Download -> top-level/default page
- Download -> Mod
- Settings -> default landing page and sidebar inventory
- Tools -> top-level/default page
- Tools -> Help

## Global Gaps

- The right-side app still exposes migration/debug surfaces in normal navigation. This is visible on the default `下载` and `工具` pages, which still render migration placeholder content instead of launcher pages.
- The right-side app mixes English migration text with Chinese launcher UI. The gold app is consistently Chinese on the tested surfaces.
- The right-side app shows extra floating UI controls at the bottom-right on multiple pages. These do not exist in the gold UI and look like shell/debug affordances rather than launcher UI.
- The right-side app still carries several shell-review panels, activity feeds, and fact cards that do not belong to the gold interaction model.

## Page Findings

### 1. Launch Home

Gold behavior:

- Left pane shows the standard empty-instance launch surface with `新建并选择一个档案以启动游戏`.
- Secondary small action buttons are present below the empty-state card.
- Primary buttons are `下载游戏` and `实例选择`.
- Community notice card contains the full longer warning text block.

Right-side gaps:

- Empty-state visual is different. It shows a fish icon card with `未选择档案`, which does not match the gold launch page structure.
- Bottom buttons are wrong. The right side shows `启动游戏`, `版本选择`, and `版本设置` instead of the gold page's `下载游戏` and `实例选择`.
- Community notice content is shortened and loses a large part of the gold warning copy.
- Top-left branding differs because the right side adds a separate `CE` badge treatment not present on the gold window.

Impact:

- This is not just visual drift. The right page presents a different empty-state workflow and different primary actions.

### 2. Launch -> Instance Selection

Gold behavior:

- Opens a dedicated `实例选择` window with the standard left file list, import actions, and centered empty-state card.
- Empty-state CTA is `下载游戏`.
- The selected `.minecraft` root is shown in the expected launcher-oriented file-list panel.

Right-side gaps:

- Opens a different shell-style surface with large explanatory cards, metadata cards, and an activity log. This does not match the gold instance selector.
- The title bar/header is visibly broken or truncated instead of cleanly presenting `实例选择`.
- The instance root path points to the repo build output under `PCL.Frontend.Avalonia/bin/Debug/net10.0/.minecraft/`, which is not an expected launcher-facing default.
- Empty-state actions differ. The right page emphasizes search/refresh/clear-filter rather than the gold page's simple empty-state flow.
- Extra floating buttons are still present on this surface.

Impact:

- Instance selection is not a parity port yet. It behaves like an internal shell page instead of the launcher selector.
- The path default is high risk because it can make the user look at the wrong runtime directory.

### 3. Download -> Default Page

Gold behavior:

- Shows the real download landing page.
- Left sidebar groups community resources and installers.
- Main content shows live Minecraft release channels and version counts.

Right-side gaps:

- Default `下载` page is still a migration placeholder.
- Main content is an English shell-review card: `Download migration surface`.
- The page shows internal shell facts such as surface type, sidebar route counts, and queued prompts instead of a user-facing launcher page.
- The page does not match the gold page structure, copy, or purpose.

Impact:

- This is a route-level blocker. The default download landing page is still the wrong page.

### 4. Download -> Mod

Gold behavior:

- Search and filter area matches the original launcher layout.
- Result cards use the gold visual hierarchy and information density.
- Listing content and metadata presentation follow launcher styling.

Right-side gaps:

- The right page has data, but the card layout still differs significantly from the gold page.
- Result rows use a flatter shell list layout and lose much of the original card styling and hierarchy.
- English descriptions are still exposed in the visible result list.
- Result actions use generic `打开页面` buttons that do not match the gold card affordances.
- Spacing, typography, and control sizing are visibly off versus gold.

Impact:

- This route is partially implemented, but still visually and interaction-wise far from parity.

### 5. Settings -> Default Landing Page And Sidebar

Gold behavior:

- The tested default landing page opened on `管理`.
- Sidebar grouping and item naming follow the original launcher vocabulary, including entries such as `软件信息`, `软件更新`, `反馈`, `查看日志`.

Right-side gaps:

- Default landing page is different. The right side opens on `启动`, not `管理`.
- Sidebar ordering/grouping differs from gold.
- Several labels are shortened or changed, for example `关于`, `日志`, `反馈`, `更新`, which does not match the gold naming.
- The overall settings page structure is not aligned to the gold initial landing experience.

Impact:

- Settings navigation parity is incomplete even before validating individual setting rows.

### 6. Tools -> Default Page

Gold behavior:

- Default tools page opens on the lobby page and shows the actual `大厅` content, including the failure banner and agreement/terms card.

Right-side gaps:

- Default tools page is still a migration placeholder.
- Main content is an English shell-review card: `Tools migration surface`.
- The right page shows shell metadata instead of the real lobby/default tools page.

Impact:

- This is another route-level blocker. Default tools navigation lands on the wrong page.

### 7. Tools -> Help

Gold behavior:

- Help page uses accordion-style grouped sections such as `指南`, `Minecraft`, `百科`, `个性化`, `启动器`.
- The page visually matches the launcher's help-library layout.

Right-side gaps:

- Right help page is partially populated, but the structure is different.
- Content is shown as flatter card sections instead of the gold accordion layout.
- The visible category inventory is smaller and does not match the gold page structure.
- Visual density, spacing, and grouping differ noticeably from the original help library.

Impact:

- Help is partially migrated, but not yet a faithful port of the original page.

## Summary

The right-side implementation currently has two different classes of gap:

- route blockers: `下载` default page and `工具` default page still open migration placeholders instead of the real launcher pages
- partial parity pages: `Mod`, `帮助`, `设置`, and `实例选择` exist but still differ substantially in structure, controls, copy, or default behavior

The highest-priority user-facing issues from this pass are:

1. Wrong default pages in `下载` and `工具`.
2. Launch page uses the wrong empty-state controls and copy.
3. Instance selection opens a shell/debug-style page and targets the wrong `.minecraft` root.
4. English migration/debug text is still visible in normal end-user routes.

## Not Covered In This Pass

I did not manually validate the following in this pass:

- individual installer subpages beyond `Mod`
- version detail pages with a real selected instance
- settings subpages beyond the default landing page inventory
- tools test surface
- logs, saves, and task-manager utility routes

Those should be covered in a follow-up pass after the route blockers above are fixed, especially once the app is pointed at a real launcher data directory with at least one usable instance.
