# Shared WPF UI Migration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Replace the native HotD2 trainer window with the shared `Vholf.Trainer.UI` WPF shell while preserving every bridge command, state rule, and release artifact.

**Architecture:** Keep `Hotd2TrainerBridge.dll` and its named-pipe protocol unchanged. Add a .NET 8 WPF client that implements the shared `ITrainerSession`, inserts a HotD-specific multi-column control panel into the reusable shell, and publishes as the existing `Hotd2RemakeTrainer.exe`.

**Tech Stack:** C# 12, .NET 8 WPF, `Vholf.Trainer.UI`, named pipes, BepInEx 5 bridge.

## Global Constraints

- Work on `feat/ui-migration`; do not alter `main`.
- Pin the published `Vholf.Trainer.UI` package for reproducible local and GitHub builds.
- Preserve `STATE` and `ACTION` wire formats exactly.
- Preserve app-wins-on-first-connect, persistent-cheat shutdown, unlock confirmation, and 2-16 shots/sec behavior.
- Keep release filenames `Hotd2RemakeTrainer.exe` and `Hotd2TrainerBridge.dll`.
- Keep a self-contained Windows x64 GUI executable.

---

### Task 1: Reusable shell extension

**Files:**
- Modify: `..\..\vholf-trainer-ui\src\Vholf.Trainer.UI\TrainerIdentity.cs`
- Modify: `..\..\vholf-trainer-ui\src\Vholf.Trainer.UI\TrainerShellViewModel.cs`
- Modify: `..\..\vholf-trainer-ui\src\Vholf.Trainer.UI\TrainerWindow.xaml`
- Test: `..\..\vholf-trainer-ui\tests\Vholf.Trainer.UI.Tests\Program.cs`

**Interfaces:**
- `TrainerIdentity` supplies consumer-specific header detail and banner stretch.
- `TrainerShellViewModel` optionally exposes consumer content.
- `TrainerWindow` renders existing option rows when no custom content exists.

- [x] Add failing identity/custom-content tests.
- [x] Run shared tests and confirm compile failure.
- [x] Add backward-compatible identity defaults and optional custom content.
- [x] Bind the shell header, banner, and body to those properties.
- [x] Run shared tests and the Dead Space build.

### Task 2: HotD protocol and session

**Files:**
- Create: `src\Hotd2RemakeTrainer.App\Protocol\TrainerState.cs`
- Create: `src\Hotd2RemakeTrainer.App\Protocol\TrainerProtocol.cs`
- Create: `src\Hotd2RemakeTrainer.App\Game\Hotd2TrainerSession.cs`
- Create: `tests\Hotd2RemakeTrainer.Tests\Program.cs`

**Interfaces:**
- `TrainerProtocol.ParseState(string)` validates eleven state values.
- `TrainerProtocol.FormatState(TrainerState)` preserves the bridge command.
- `TrainerProtocol.FormatAction(int)` accepts only existing unlock IDs.
- `Hotd2TrainerSession` implements `ITrainerSession` and owns connection/state synchronization.

- [x] Add failing protocol tests covering round-trip, invalid radio combinations, range rejection, and action IDs.
- [x] Run tests and confirm compile failure.
- [x] Implement the minimum protocol model/parser.
- [x] Run protocol tests.
- [x] Add session state-change and first-connect precedence tests.
- [x] Implement named-pipe polling, state push, action dispatch, and shutdown cleanup.
- [x] Run all tests.

### Task 3: HotD control panel

**Files:**
- Create: `src\Hotd2RemakeTrainer.App\Presentation\Hotd2TrainerPanel.xaml`
- Create: `src\Hotd2RemakeTrainer.App\Presentation\Hotd2TrainerPanel.xaml.cs`
- Create: `src\Hotd2RemakeTrainer.App\App.xaml`
- Create: `src\Hotd2RemakeTrainer.App\App.xaml.cs`
- Create: `src\Hotd2RemakeTrainer.App\Hotd2RemakeTrainer.App.csproj`

**Interfaces:**
- Panel binds to `Hotd2TrainerSession`.
- Gameplay and persistence switches call `UpdateStateAsync`.
- Fire selection is exclusive; rapid-fire rate remains 2-16.
- Progression buttons call `SendActionAsync`; achievements require confirmation.

- [x] Add the WPF project and embedded HotD artwork.
- [x] Build a two-column panel using the shared shell palette.
- [x] Wire switches, fire mode, rate control, persistence, and actions.
- [x] Add `--self-test` dispatch.
- [x] Build and run tests.

### Task 4: Build, docs, and rendered verification

**Files:**
- Modify: `build.ps1`
- Modify: `README.md`
- Remove from build: `Hotd2RemakeTrainer.cpp`, `Hotd2TrainerResources.rc`
- Update: `dist\Hotd2RemakeTrainer.exe`
- Update: `dist\Hotd2TrainerBridge.dll`

**Interfaces:**
- `build.ps1` builds tests, publishes the WPF app, and builds the bridge locally; `-SkipBridge` supports GitHub where game assemblies are unavailable.
- Release workflow continues publishing the same two checked-in files.

- [x] Update build and architecture documentation.
- [x] Run shared UI tests, Dead Space build, HotD tests, HotD build, and `--self-test`.
- [x] Launch the trainer, capture the window, and inspect all control states.
- [x] Confirm both worktrees contain only intended changes.
