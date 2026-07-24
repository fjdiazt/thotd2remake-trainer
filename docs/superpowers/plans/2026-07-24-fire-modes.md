# Fire Modes and One Shot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add mutually exclusive Off, Auto Fire, and human-rate Rapid Fire modes plus One Shot Mode.

**Architecture:** Keep the existing HasAutoFire hook for Auto Fire. Add an eight-shots-per-second postfix on `CR_Player.handleAutoFire()` that invokes normal `CR_Player.Fire()` for Rapid Fire. Extend the existing pipe/config state for Rapid Fire and One Shot.

**Tech Stack:** Win32 C++17, BepInEx 5.4.22, HarmonyX, C# netstandard2.1

## Global Constraints

- Add no dependency.
- Fire modes must be mutually exclusive.
- Rapid Fire must call normal `CR_Player.Fire()` and retain native cooldown.
- Old pipe commands remain accepted with new values disabled.
- Persist and synchronize both new values.

---

### Task 1: Fire-mode radio group and One Shot

**Files:**
- Modify: `Hotd2RemakeTrainer.cpp`
- Modify: `Hotd2TrainerBridge.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: `CR_Player.handleAutoFire()`, `CR_Player.Fire()`, `MP_PlayerInput.IsHoldingFire`, `CR_Cheats` One Shot field
- Produces: `STATE health ammo continues auto persist rapid oneShot`

- [x] **Step 1: Extend native self-test first**

Require a valid seven-value state:

```cpp
std::strcmp(command, "STATE 1 0 1 1 1 0 1\n") == 0
```

and reject `auto == true && rapid == true`.

Run `.\build.ps1`, then run the GUI self-test with `Start-Process -Wait`.
Expected before implementation: exit code `1`.

- [x] **Step 2: Implement native GUI and protocol**

Replace Turbo with Off/Auto/Rapid radio buttons. Add One Shot. Parse and emit
seven state values. Preserve pending-local-state precedence and persistence
close behavior.

- [x] **Step 3: Implement bridge modes**

Keep `ForceAutoFire` for Auto. Patch `CR_Player.handleAutoFire()` with a
postfix that calls normal `CR_Player.Fire()` no more than every 125 ms while
Rapid is enabled and the normal trigger is held. Add, apply, restore, save,
load, parse, and emit One Shot and Rapid values.

- [x] **Step 4: Build and deploy**

Run:

```powershell
.\build.ps1 -Deploy
```

Expected: zero warnings and errors.

- [x] **Step 5: Verify behavior**

- Self-test exit `0`.
- Pipe smoke tests synchronize Off, Auto, and Rapid radios plus One Shot.
- Static assembly check finds `CR_Player.Fire`, `handleAutoFire`,
  `MP_PlayerInput.IsHoldingFire`, and the One Shot backing field.
- Built and deployed bridge hashes match.
- `git diff --check` exits `0`.

- [x] **Step 6: Commit**

```powershell
git add Hotd2RemakeTrainer.cpp Hotd2TrainerBridge.cs README.md `
  dist/Hotd2RemakeTrainer.exe dist/Hotd2TrainerBridge.dll `
  docs/superpowers/plans/2026-07-24-fire-modes.md
git commit -m "feat: add fire modes and one shot"
```
