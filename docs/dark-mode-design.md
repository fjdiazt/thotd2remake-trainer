# Dark Mode Design

## Goal

Render the existing trainer in a fixed dark theme without changing layout,
controls, cheat behavior, persistence, or bridge communication.

## Design

- Keep the existing native Win32 control tree and dimensions.
- Use one dark background brush plus fixed background, panel, text, muted-text,
  border, and accent colors.
- Handle `WM_CTLCOLORSTATIC`, `WM_CTLCOLOREDIT`, and `WM_CTLCOLORBTN` so child
  controls inherit the palette.
- Enable Windows' immersive dark title bar and apply the native dark Explorer
  theme to common controls.
- Delete the brush during shutdown.

## Verification

- Extend `--self-test` to exercise the palette contract before implementation.
- Build the executable and run `Hotd2RemakeTrainer.exe --self-test`.
- Launch the trainer and capture the rendered window to verify dark background,
  readable text, disabled states, groups, buttons, radios, checkboxes, and
  spinner.

## Constraints

- No new dependency.
- No layout or behavior changes.
- Windows 10/11 only, matching the existing application.
