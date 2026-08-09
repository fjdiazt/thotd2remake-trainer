# Dark Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render the existing Win32 trainer in a fixed, readable dark theme.

**Architecture:** Keep the current control tree. Centralize the palette and
theme setup in `Hotd2RemakeTrainer.cpp`; paint native controls through Win32
color messages and Windows dark-theme APIs.

**Tech Stack:** C++17, Win32, UxTheme, DWM.

## Global Constraints

- No new third-party dependency.
- No layout, cheat, persistence, or bridge behavior changes.
- Keep Windows 10/11 compatibility.

---

### Task 1: Native dark theme

**Files:**
- Modify: `Hotd2RemakeTrainer.cpp`
- Modify: `build.ps1`

**Interfaces:**
- Consumes: existing `WindowProc`, child control handles, and `SelfTest`.
- Produces: `ApplyDarkTheme(HWND)` and dark control painting.

- [x] **Step 1: Write the failing self-test**

Add a palette invariant to `SelfTest()` that calls `IsDarkPalette()` and
requires the background luminance to be below the text luminance.

- [x] **Step 2: Run test to verify it fails**

Run: `.\build.ps1; .\dist\Hotd2RemakeTrainer.exe --self-test`

Expected: compiler failure because `IsDarkPalette` does not exist.

- [x] **Step 3: Write minimal implementation**

Add fixed color constants, the palette invariant, one background brush,
`WM_CTLCOLORSTATIC` / `WM_CTLCOLOREDIT` / `WM_CTLCOLORBTN` handlers,
`SetWindowTheme` calls for native controls, `DwmSetWindowAttribute` for the
title bar, and brush cleanup. Link `uxtheme.lib` and `dwmapi.lib`.

- [x] **Step 4: Run automated verification**

Run: `.\build.ps1; .\dist\Hotd2RemakeTrainer.exe --self-test`

Expected: build exit 0 and self-test exit 0.

- [x] **Step 5: Verify rendered UI**

Launch `dist\Hotd2RemakeTrainer.exe`, capture its window, and inspect the
background, text, disabled buttons, checkboxes, radio buttons, group boxes,
and spinner.

- [x] **Step 6: Commit**

Stage only `Hotd2RemakeTrainer.cpp`, `build.ps1`, and this plan. Commit:
`feat: add native dark theme`.

---

### Task 2: Embedded artwork header

**Files:**
- Create: `assets/hotd2-header.jpg`
- Create: `Hotd2TrainerResources.rc`
- Modify: `Hotd2RemakeTrainer.cpp`
- Modify: `build.ps1`
- Modify: `dist/Hotd2RemakeTrainer.exe`

**Interfaces:**
- Consumes: `IDR_HEADER_JPG`, the existing dark background color, and
  `WindowProc`.
- Produces: `CalculateTopCoverCrop(...)`, embedded JPEG loading, and header
  painting during `WM_PAINT`.

- [x] **Step 1: Write the failing crop test**

Extend `SelfTest()` with hand-calculated assertions that a 1280x733 image
covering a 776x220 banner yields source rectangle `(0, 0, 1280, 363)`.

- [x] **Step 2: Verify red**

Run: `.\build.ps1`

Expected: compiler failure because `CalculateTopCoverCrop` does not exist.

- [x] **Step 3: Add embedded resource and renderer**

Copy the supplied JPEG to `assets/hotd2-header.jpg`. Compile it as `RCDATA`.
Use Windows GDI+ to decode the embedded JPEG once, draw the top-anchored cover
crop at 220 pixels high, then overlay a 70-pixel dark gradient. Shift existing
controls and window height down 200 pixels.

- [x] **Step 4: Verify green**

Run: `.\build.ps1; .\dist\Hotd2RemakeTrainer.exe --self-test`

Expected: build exit 0, zero warnings, self-test exit 0, and no external image
beside the executable.

- [x] **Step 5: Verify rendered UI**

Launch `dist\Hotd2RemakeTrainer.exe`; capture and inspect the header crop,
gradient, dark theme, full control layout, and 776-pixel client width.

- [x] **Step 6: Commit**

Stage the source, embedded JPEG, resource script, build output, executable,
and plan. Commit: `feat: add faded artwork header`.
