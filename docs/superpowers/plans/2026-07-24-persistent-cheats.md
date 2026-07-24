# Persistent Cheats Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remember cheat state in the bridge, restore it on game launch, and synchronize all GUI checkboxes on connection.

**Architecture:** BepInEx config is the only persisted store. The named pipe becomes duplex: the bridge sends `STATE health ammo continues turbo persist` immediately after connection, then accepts the same format from the GUI.

**Tech Stack:** Win32 C++17, BepInEx 5.4.22, C# netstandard2.1

## Global Constraints

- Add no dependency.
- Keep old four- and five-part commands compatible with persistence off.
- Invalid or missing initial state must disconnect and retry, never overwrite bridge state.
- `Remember cheats across game restarts` checked means GUI close only disconnects.

---

### Task 1: Persist and synchronize trainer state

**Files:**
- Modify: `Hotd2RemakeTrainer.cpp`
- Modify: `Hotd2TrainerBridge.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: named pipe `Hotd2RemakeTrainer`, BepInEx `Config`
- Produces: `STATE health ammo continues turbo persist`

- [ ] **Step 1: Make the native protocol self-test fail**

Extend `SelfTest()` to require formatting and parsing:

```cpp
std::strcmp(command, "STATE 1 0 1 1 1\n") == 0
```

Run:

```powershell
.\build.ps1
.\dist\Hotd2RemakeTrainer.exe --self-test
```

Expected before implementation: exit code `1`.

- [ ] **Step 2: Implement duplex GUI synchronization**

Add the fifth checkbox, five-value formatter/parser, `GENERIC_READ |
GENERIC_WRITE`, initial bridge-state read, and:

```cpp
if (!IsChecked(gPersist))
    SendState(false, false, false, false, false);
```

Initial state read must complete before `SendCurrentState()`.

- [ ] **Step 3: Implement bridge persistence**

Bind BepInEx config entries for `Persist`, `InfiniteHealth`,
`InfiniteAmmo`, `InfiniteContinues`, and `Turbo`. Load them in `Awake()`
only when `Persist` is true. On each accepted pipe state, update desired
state; save config from `Update()`.

Change the server to `PipeDirection.InOut` and write:

```text
STATE health ammo continues turbo persist
```

before reading GUI commands.

- [ ] **Step 4: Build and deploy**

```powershell
.\build.ps1 -Deploy
.\dist\Hotd2RemakeTrainer.exe --self-test
```

Expected: zero build warnings/errors and self-test exit code `0`.

- [ ] **Step 5: Verify and commit**

Verify the built/deployed bridge hashes match, `git diff --check` exits
zero, and the pipe sync smoke test returns five checkbox values.

```powershell
git add Hotd2RemakeTrainer.cpp Hotd2TrainerBridge.cs README.md `
  dist/Hotd2RemakeTrainer.exe dist/Hotd2TrainerBridge.dll `
  docs/superpowers/plans/2026-07-24-persistent-cheats.md
git commit -m "feat: persist cheat state"
```
