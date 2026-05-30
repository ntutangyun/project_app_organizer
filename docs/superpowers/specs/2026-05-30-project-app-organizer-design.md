# Project App Organizer — Design

**Date:** 2026-05-30
**Status:** Approved for implementation
**Platform:** Windows 11, C# / .NET (WPF)

## Problem

When working across multiple projects on Windows, each project spans several
windows — git bash, cmd, PowerShell, Claude Code sessions, plus IDEs (VS Code,
DevEco Studio), File Explorer, browser tabs, Outlook, etc. Terminal titles carry
no useful project context, so switching projects means visually scanning the
contents of many windows to re-orient. This is slow and mentally expensive.

## Goal

A desktop app that groups all of a project's windows together and lets you switch
project context instantly, with each project's title, description, and todo list
always visible.

## Core model: Docker / stage

The app does **not** embed external windows (no `SetParent` reparenting — too
fragile). Instead it reserves a **stage** region on screen and snaps the active
window into it:

- Selecting a window's tab restores + moves + resizes that real OS window to fill
  the stage rectangle, and focuses it.
- The **previously staged window is minimized**, so only the active tab is visible.
- The app window spans the screen *behind* the stage; because the external window
  covers only the stage sub-rectangle, the sidebar and tab bar remain visible.
- If the app window is moved / resized / minimized / restored, the staged window
  follows it as a unit.

## Main window layout (Split sidebar)

```
┌──────────┬───────────────┬────────────────────────────────┐
│ PROJECTS │ App Organizer │ [git bash][pwsh][VS Code][ + ]  │  ← tab bar
│ Mobile   │ <description> ├────────────────────────────────┤
│ ▸Organizr│               │                                │
│ Website  │ TODO          │            STAGE               │
│          │ ✓ Pick stack  │     (active window snaps here) │
│          │ ▢ Window snap │                                │
│          │ ▢ Tab bar     │                                │
└──────────┴───────────────┴────────────────────────────────┘
   rail        detail panel              main area
```

- **Project rail** (far left): list of projects; click to switch. Add/remove project.
- **Detail panel**: selected project's **title**, **description**, and **todo list**
  (checkable items; add / edit / reorder / delete).
- **Main area**: **tab bar** of the project's windows + the **stage** below.

## Window management

### Terminals — launched from the app
- Ownership known at spawn time (no cwd-sniffing).
- Each project stores reusable **terminal profiles**: `{ name, shell (git bash |
  cmd | powershell), workingDirectory, startupCommand? }`. One-click relaunch.
- Plus an ad-hoc `+` menu to spawn a fresh git bash / cmd / PowerShell in the
  project's folder.
- Launcher resolves the spawned process's main window handle (waits for the window
  to appear) so it can be staged.

### Other apps — attached via picker list
- A **picker list** enumerates currently-open real top-level windows (title + icon,
  multi-select). User picks which to attach to the active project.
- Covers IDEs, File Explorer, browsers, Outlook, PowerPoint, etc.

### Stage behavior on tab switch
- Restore + `SetWindowPos` the selected window into the stage rect + foreground it.
- Minimize the previously staged window.

### Closing a tab (smart per type)
- **Terminal tab** → closes the terminal process.
- **Attached-app tab** → detaches only (window returns to floating freely); never
  closes the external app.

## Technical components

- **Win32 interop layer** — P/Invoke wrappers: `EnumWindows`, `GetWindowText`,
  `GetWindowThreadProcessId`, `IsWindowVisible`, `GetWindow`, `SetWindowPos`,
  `ShowWindow` (minimize/restore), `SetForegroundWindow`, `GetWindowRect`, window
  icon + PID lookup. DPI-aware. Hidden behind an `IWindowManager` interface so the
  rest of the app — and tests — never touch Win32 directly.
- **Stage manager** — computes the stage rectangle in screen coordinates from the
  WPF stage element; drives snap / minimize / follow logic; keeps the active window
  aligned when the app window moves or resizes.
- **Terminal launcher** — spawns shells with the right working dir + optional
  startup command; detects git bash path; resolves the new main window handle.
- **Window picker** — enumerates real top-level windows, filtering out tool
  windows, hidden / titleless windows, and the organizer's own window.
- **Project store + view models** — MVVM (project list, project detail, todo, tabs).

## Data & persistence

- Local **JSON** under `%APPDATA%`.
- **Persisted per project**: id, name, description, folder path, ordered todo items
  `{ text, done }`, terminal profiles.
- **Not persisted**: live attachments to external windows (HWNDs don't survive
  restarts). On reopen: project, todos, and terminal profiles are restored;
  relaunch terminals in one click; re-attach external apps as needed.

## Scope

**v1**
- Single project organizer window.
- Single monitor (the monitor the app is on).
- Manual project / todo / profile management.

**Future (out of scope for v1)**
- Multi-monitor stages.
- Drag-to-reorder tabs across projects.
- True embedding for cooperative apps.
- Auto re-attach of external windows by heuristic.

## Testing strategy

- **TDD** for: project model, JSON persistence, todo logic, view-model behavior,
  and stage/snap logic tested against a **fake `IWindowManager`**.
- The real Win32 interop layer is verified **manually** (hard to unit test
  reliably), kept thin so most logic is testable.
