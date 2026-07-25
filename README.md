# The House of the Dead 2: Remake Trainer

![The House of the Dead 2: Remake logo](assets/hotd2-remake-logo.png)

Portable Windows trainer for **THE HOUSE OF THE DEAD 2: Remake**. Instead of
patching raw game memory like a conventional standalone trainer, it loads a
small BepInEx bridge into the game. This lets it use the game's built-in cheat
system while the separate GUI only controls the options.

The bridge also owns persistence. Enable **Remember cheats across game
restarts**, choose the options once, and the bridge restores them on every
later game launch; the trainer GUI does not need to be started again. No Cheat
Engine or process-memory editor is required.

## Features

- Infinite Health
- Infinite Ammo
- Infinite Continues
- One Shot Mode
- Auto Fire at the weapon's native maximum rate
- Adjustable Rapid Fire from 2 to 16 shots per second
- Optional cheat persistence across game and trainer restarts

## Requirements

- Windows version of **THE HOUSE OF THE DEAD 2: Remake**
- 64-bit [BepInEx 5.4.22](https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip)

Use BepInEx 5 x64. Do not install the x86 package or BepInEx 6.

## Install BepInEx

1. In Steam, right-click the game and select **Manage > Browse local files**.
2. Download the BepInEx archive linked above.
3. Extract the archive directly into the folder containing
   `THE HOUSE OF THE DEAD 2 Remake.exe`.
4. Confirm the game folder now contains:

   ```text
   BepInEx\
   doorstop_config.ini
   winhttp.dll
   THE HOUSE OF THE DEAD 2 Remake.exe
   ```

5. Start the game once, reach the menu, then close it. BepInEx creates its
   remaining folders and configuration files during this first run.

`winhttp.dll` must keep that exact filename. Renaming or removing it disables
BepInEx.

## Install the trainer

1. Download or clone this repository.
2. Copy `dist\Hotd2TrainerBridge.dll` to:

   ```text
   <game folder>\BepInEx\plugins\Hotd2TrainerBridge.dll
   ```

3. Keep `dist\Hotd2RemakeTrainer.exe` anywhere convenient.
4. Fully restart the game after installing or replacing the bridge DLL.

## Run

Start `Hotd2RemakeTrainer.exe` before or after starting the game. The status
changes to **Connected to Remake** when the in-game bridge is ready.

If the trainer is opened first, it waits for the game. Options selected before
the connection are sent to the bridge when the game starts.

## Options

| Option | Behavior |
|---|---|
| Infinite Health | Keeps player health protected. |
| Infinite Ammo | Enables the game's infinite-ammo state. |
| Infinite Continues | Enables unlimited continue tokens. |
| One Shot Mode | Enables the game's built-in one-shot damage mode. |
| Off | Disables both automatic fire modes. |
| Auto Fire (native max) | Holding the trigger uses the game's built-in automatic-fire loop at the weapon's native maximum cadence. |
| Rapid Fire | Holding the trigger repeats the normal fire action at the selected 2-16 shots-per-second rate. Native ammo, reload, blocking, and weapon cooldown rules still apply. |
| Remember cheats across game restarts | Saves every option in the bridge and restores it the next time the game starts. |

Auto Fire and Rapid Fire are mutually exclusive. Rapid Fire does not modify
per-shot damage; any faster enemy kills come from firing more often.

## Persistence

With **Remember cheats across game restarts** enabled:

- closing the GUI leaves the selected cheats active;
- starting the game restores them without opening the GUI;
- reopening the GUI synchronizes its controls from the bridge.

With persistence disabled, closing the GUI turns all trainer options off.

Saved state is stored in:

```text
<game folder>\BepInEx\config\local.hotd2remake.trainerbridge.cfg
```

## Troubleshooting

### Waiting for Remake

The game process was not found. Start the Remake and confirm the executable is
named `THE HOUSE OF THE DEAD 2 Remake.exe`.

### Game found; BepInEx bridge offline

The game is running, but the plugin did not load.

1. Close the game completely.
2. Confirm `winhttp.dll` exists beside the game executable.
3. Confirm `Hotd2TrainerBridge.dll` exists in `BepInEx\plugins`.
4. Start the game again.

The bridge loads only when the game starts. Replacing its DLL while the game is
already running requires a full game restart.

### GUI and bridge do not connect after an update

Use `Hotd2RemakeTrainer.exe` and `Hotd2TrainerBridge.dll` from the same build.
The two files share a state protocol and should be updated together.

## Build from source

Release binaries in `dist` need no developer tools. Building requires the
.NET SDK, Visual Studio C++ tools, and a local game installation.

```powershell
.\build.ps1
```

Outputs:

```text
dist\Hotd2RemakeTrainer.exe
dist\Hotd2TrainerBridge.dll
```

Build and deploy the bridge to the default game path:

```powershell
.\build.ps1 -Deploy
```

For another Steam library, supply the game folder:

```powershell
.\build.ps1 -GameRoot 'D:\SteamLibrary\steamapps\common\THE HOUSE OF THE DEAD 2 Remake' -Deploy
```
