# SideDock — Roadmap

We build **one phase at a time**. Each phase is completed, tested, and confirmed
working before the next begins.

Core constraint for every phase: **standalone app, documented APIs only, no
process injection, no modifying the system taskbar or other system components.**

---

> **Status:** All five phases complete (v0.1.0). Future ideas are listed at the
> bottom under "Beyond v0.1.0".

## Phase 1 — Docked vertical bar ✅
A left-edge, vertical, **always-on-top**, **borderless** bar window that docks to
the screen edge and **reserves its space** so maximized windows don't overlap it.

- Fixed width (e.g. 64 px), full usable screen height.
- No title bar / no border.
- Uses the documented **AppBar API** (`SHAppBarMessage` with `ABM_NEW` /
  `ABM_QUERYPOS` / `ABM_SETPOS` / `ABM_REMOVE`) to reserve screen space — **not**
  by forcing or hiding the system taskbar.
- Just an empty, solidly styled bar for now.

**Done when:** the bar appears on the left edge, stays on top, and a maximized
window leaves a 64 px gap for it.

---

## Phase 2 — App launcher ✅
- Read a list of apps (path + optional icon/label) from a **config file**
  (e.g. JSON) in the user's profile.
- Show their icons stacked vertically in the bar.
- **Click an icon → launch that app** (`Process.Start`).

---

## Phase 3 — Clock + system tray ✅
- A small **clock** (and maybe date) on the bar, updating on a timer.
- A **notification-area / tray** presence so the dock can be shown/hidden and
  exited cleanly.

---

## Phase 4 — Open windows (taskbar-like) ✅
- Enumerate top-level **open windows** using documented Win32 APIs
  (`EnumWindows`, `GetWindowText`, `IsWindowVisible`, etc.).
- Show a button per window; click to **activate** that window
  (`SetForegroundWindow`).
- Keep the list in sync as windows open/close.

---

## Phase 5 — Customization + performance ✅
- User-configurable **theme** (colors/transparency), **size** (thickness/icon
  size), **fonts**, and **position** (Left / Right edge).
- Settings persisted to the config file, with **live reload** on edit.
- **Performance pass:** timers pause while hidden, window icons fetched lazily,
  brushes frozen, window list rebuilt only when it changes.

> Note: only Left/Right edges are supported (both vertical). Top/Bottom
> (horizontal) was intentionally deferred — see below.

---

## Beyond v0.1.0 (future ideas)
- Top / Bottom (horizontal) docking.
- Multi-monitor and per-monitor DPI awareness.
- Custom application icon for the window/tray (currently the generic icon).
- Optional "start with Windows" setting.
- Event-driven open-windows updates (`SetWinEventHook`) instead of polling.
- Drag-and-drop to add apps; reordering.
