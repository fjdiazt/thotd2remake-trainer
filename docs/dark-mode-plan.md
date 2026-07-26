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

- [ ] **Step 1: Write the failing self-test**

Add a palette invariant to `SelfTest()` that calls `IsDarkPalette()` and
requires the background luminance to be below the text luminance.

- [ ] **Step 2: Run test to verify it fails**

Run: `.\build.ps1; .\dist\Hotd2RemakeTrainer.exe --self-test`

Expected: compiler failure because `IsDarkPalette` does not exist.

- [ ] **Step 3: Write minimal implementation**

Add fixed color constants, the palette invariant, one background brush,
`WM_CTLCOLORSTATIC` / `WM_CTLCOLOREDIT` / `WM_CTLCOLORBTN` handlers,
`SetWindowTheme` calls for native controls, `DwmSetWindowAttribute` for the
title bar, and brush cleanup. Link `uxtheme.lib` and `dwmapi.lib`.

- [ ] **Step 4: Run automated verification**

Run: `.\build.ps1; .\dist\Hotd2RemakeTrainer.exe --self-test`

Expected: build exit 0 and self-test exit 0.

- [ ] **Step 5: Verify rendered UI**

Launch `dist\Hotd2RemakeTrainer.exe`, capture its window, and inspect the
background, text, disabled buttons, checkboxes, radio buttons, group boxes,
and spinner.

- [ ] **Step 6: Commit**

Stage only `Hotd2RemakeTrainer.cpp`, `build.ps1`, and this plan. Commit:
`feat: add native dark theme`.
