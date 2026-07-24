# Turbo Fire Design

## Goal

Add an optional `Turbo / Continuous Fire` trainer toggle. While enabled,
holding the normal fire control repeatedly shoots at the equipped weapon's
native fire rate.

## Architecture

The game already calls `CR_Player.handleAutoFire()` every frame. That path
checks `MP_PlayerInput.IsHoldingFire`, `CR_WeaponHolder.HasAutoFire`, and then
calls `CR_WeaponHolder.FireWeapon()`. The bridge will use one Harmony postfix
on the `HasAutoFire` getter and force only its returned value to `true` while
Turbo is enabled.

This keeps the game's existing fire cooldown, ammo use, infinite-ammo cheat,
reload behavior, sound, recoil, input blocking, and weapon-switch handling.
Disabling Turbo immediately returns the getter's untouched native result.

## Components

- Native GUI: add one checkbox and send a fourth boolean in the existing
  `STATE` pipe command.
- BepInEx bridge: parse the fourth boolean, store it thread-safely, and apply
  the Harmony postfix.
- Build: reference the installed `0Harmony.dll`; no new dependency download.

## Compatibility

The bridge accepts both old three-value and new four-value state commands.
Old commands mean Turbo off. The GUI always emits the new four-value command.

If `CR_WeaponHolder.HasAutoFire` cannot be found, the bridge logs an error and
continues serving the existing health, ammo, and continues toggles.

## Verification

- Build with zero warnings/errors.
- Confirm `CR_WeaponHolder.HasAutoFire` exists in the installed game assembly.
- Extend the executable self-test for the new protocol.
- Run named-pipe smoke tests for Turbo off and on commands.
- Deploy bridge and confirm deployed DLL hash matches the build artifact.
- Runtime firing validation requires the user to hold fire in the game.
