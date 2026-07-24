# HotD2 Remake Trainer Implementation Plan

**Goal:** Build and deploy a portable GUI plus BepInEx bridge.

**Architecture:** Native x64 Win32 GUI sends three booleans through a named
pipe. Managed bridge applies them to the game's built-in cheat fields on the
Unity main thread.

- [ ] Build `Hotd2TrainerBridge.dll` against installed BepInEx and Unity.
- [ ] Build statically linked `Hotd2RemakeTrainer.exe`.
- [ ] Deploy bridge to `BepInEx\plugins`.
- [ ] Run GUI self-test and inspect PE architecture/imports.
- [ ] Leave `winhttp.dll-` unchanged for user-controlled enablement.
