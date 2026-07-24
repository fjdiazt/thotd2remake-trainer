# Fire Modes and One Shot Design

## Goal

Let the player choose between the current native-max automatic firing and a
human-rate trigger repeater, and expose the game's existing One Shot Mode.

## UI

- Add `One Shot Mode`.
- Replace the Turbo checkbox with one radio group:
  - `Off`
  - `Auto Fire (native max)`
  - `Rapid Fire (8 shots/sec)`
- Keep `Remember cheats across game restarts`.

## Fire Modes

`Auto Fire` keeps the current `CR_WeaponHolder.HasAutoFire` postfix. The game
calls `FireWeapon()` whenever fire is held and the weapon fires whenever its
native cooldown permits.

`Rapid Fire` does not change `HasAutoFire`. A postfix on
`CR_Player.handleAutoFire()` checks the normal held-trigger state and calls
the same `CR_Player.Fire()` method registered to physical trigger presses,
at most eight times per second. `CR_Player.Fire()` and `CR_Weapon.Fire(false)`
retain readiness, ammo, reload, blocked-input, death, sound, recoil, and native
weapon-cooldown checks.

The modes are mutually exclusive. `Off` applies neither behavior.

## One Shot

Use `<isCheatOneShotModeActive>k__BackingField` from `CR_Cheats`. The game
already applies `999` damage when `CheatType.OneShotMode` is active. Restore
the original field value when the bridge unloads.

## State and Compatibility

Extend the pipe state to:

`STATE health ammo continues auto persist rapid oneShot`

The new GUI emits seven values. The bridge continues accepting the old
three-, four-, and five-value forms with omitted values disabled. Reject a
state that enables both fire modes.

Persist `Turbo` as the Auto Fire value for configuration compatibility. Add
`RapidFire` and `OneShot` settings. Bridge-to-GUI synchronization includes
all seven values.

## Verification

- Native self-test covers seven-value formatting, parsing, and invalid dual
  fire modes.
- Red/green pipe test covers radio synchronization.
- Build with zero warnings and errors.
- Confirm required game methods and One Shot field in `Assembly-CSharp.dll`.
- Deploy and compare bridge hashes.
