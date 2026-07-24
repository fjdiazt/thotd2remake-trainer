# Persistent Cheats Design

## Goal

Add a `Remember cheats across game restarts` checkbox. When enabled, the
bridge remembers all four cheat toggles, keeps them active after the GUI
closes, and restores them on the next game launch.

## Design

- BepInEx config is the single source of persisted state.
- Extend the pipe state to:
  `STATE health ammo continues turbo persist`.
- Change the pipe to two-way. On connection, the bridge sends its current
  state before the GUI sends anything. The GUI updates all five checkboxes
  from that state.
- When persistence is enabled, the bridge stores the four cheat values and
  the persistence flag through BepInEx config.
- When persistence is disabled, closing the GUI sends all cheats off.
- When persistence is enabled, closing the GUI only disconnects.
- Old four- and five-part state commands remain accepted with persistence
  disabled.

## Failure Handling

- If initial bridge state cannot be read, the GUI disconnects and retries.
- Invalid state messages are ignored.
- Missing game fields or the Turbo patch target retain existing failure
  behavior.

## Verification

- Extend the native self-test for five-value formatting and state parsing.
- Build both binaries with zero errors.
- Verify the bridge references BepInEx config and the installed bridge hash
  matches the build.
- Smoke-test bridge-to-GUI initial synchronization and close behavior.
