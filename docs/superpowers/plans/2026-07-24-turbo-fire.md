# Turbo Fire Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a trainer toggle that continuously fires while normal fire input is held, using each weapon's native fire rate.

**Architecture:** Extend the named-pipe state command from three to four booleans. The BepInEx bridge applies a Harmony postfix to `CR_WeaponHolder.HasAutoFire`, forcing only that getter result while Turbo is enabled.

**Tech Stack:** Win32 C++17, BepInEx 5.4.22, HarmonyX, C# netstandard2.1

## Global Constraints

- Preserve native weapon cooldown, reload, ammo, sound, recoil, and input guards.
- Old three-value pipe commands keep working and mean Turbo off.
- Missing patch target must not break existing trainer features.
- Use installed `BepInEx\core\0Harmony.dll`; add no dependency.

---

### Task 1: Turbo fire end to end

**Files:**
- Modify: `Hotd2RemakeTrainer.cpp`
- Modify: `Hotd2TrainerBridge.cs`
- Modify: `Hotd2TrainerBridge.csproj`
- Modify: `README.md`

**Interfaces:**
- Consumes: `CR_WeaponHolder.HasAutoFire`, normal held-fire input, pipe command `STATE health ammo continues turbo`
- Produces: fourth GUI checkbox and thread-safe Turbo override

- [ ] **Step 1: Run failing protocol check**

```powershell
Select-String Hotd2RemakeTrainer.cpp -SimpleMatch '"STATE %d %d %d %d\n"'
```

Expected: no match.

- [ ] **Step 2: Extend native protocol and GUI**

Add `FormatStateCommand(...)`, fourth boolean serialization, a
`Turbo / Continuous Fire` checkbox, and self-test assertion:

```cpp
FormatStateCommand(command, sizeof(command), true, false, true, true);
return std::strcmp(command, "STATE 1 0 1 1\n") == 0;
```

- [ ] **Step 3: Add bridge Harmony override**

Reference `0Harmony.dll`, patch the getter by reflection, and use:

```csharp
private static void ForceAutoFire(ref bool __result)
{
    if (Interlocked.CompareExchange(ref turboEnabled, 0, 0) != 0)
        __result = true;
}
```

Parse both `STATE 0 0 0` and `STATE 0 0 0 1`. Call `UnpatchSelf()` on unload.

- [ ] **Step 4: Build and deploy**

```powershell
.\build.ps1 -Deploy
```

Expected: bridge build has 0 warnings/errors; trainer builds; bridge deploys.

- [ ] **Step 5: Verify**

```powershell
Start-Process .\dist\Hotd2RemakeTrainer.exe -ArgumentList --self-test -Wait -PassThru
```

Expected: exit code `0`. Confirm installed game exposes
`CR_WeaponHolder.HasAutoFire`; smoke-test pipe output
`STATE 0 0 0 0`; compare built/deployed bridge hashes.

- [ ] **Step 6: Commit**

```powershell
git add Hotd2RemakeTrainer.cpp Hotd2TrainerBridge.cs Hotd2TrainerBridge.csproj README.md docs/superpowers/plans/2026-07-24-turbo-fire.md
git commit -m "feat: add turbo fire"
```
