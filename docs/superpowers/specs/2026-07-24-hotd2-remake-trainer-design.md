# HotD2 Remake Trainer Design

Build a portable native Windows GUI with three checkboxes. A BepInEx bridge
inside the Unity Mono game receives checkbox state over a local named pipe and
sets the Remake's existing `GodMode`, `InfiniteAmmo`, and `UnlimitedTokens`
cheat state. The user's existing BepInEx loader remains user-controlled.

The bridge avoids raw JIT hooks from the Cheat Engine table. It restores
pre-existing cheat state when unloaded. The GUI disables all three trainer
states when closed normally.
