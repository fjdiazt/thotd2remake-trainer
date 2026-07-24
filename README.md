# HotD2 Remake Trainer

Portable Windows GUI for:

- Infinite Health
- Infinite Ammo
- Infinite Continues (the game's Unlimited Tokens cheat)
- Turbo / Continuous Fire

Turbo uses the game's native held-fire loop and each weapon's native fire
rate. Ammo, reload, sound, recoil, and input blocking remain game-controlled.

The GUI talks to a small BepInEx bridge. It uses the Remake's built-in
`CR_Cheats` state instead of fragile raw memory offsets.

Enable `Remember cheats across game restarts` to store all toggle states in
the bridge. The game restores them without opening the GUI. Reopening the GUI
synchronizes every checkbox from the bridge. With persistence disabled,
closing the GUI turns all cheats off.

## Build

```powershell
.\build.ps1
```

Outputs:

- `dist\Hotd2RemakeTrainer.exe`
- `dist\Hotd2TrainerBridge.dll`

Build and copy the bridge into the installed game:

```powershell
.\build.ps1 -Deploy
```

## Run

1. Enable the existing BepInEx loader by restoring the game's
   `winhttp.dll` filename.
2. Restart the game.
3. Run `dist\Hotd2RemakeTrainer.exe`.

No Cheat Engine required.
