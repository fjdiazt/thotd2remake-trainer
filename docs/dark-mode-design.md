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

## Artwork Header

- Embed the supplied JPEG in the trainer executable; no sidecar asset.
- Draw a 220-pixel header across the client width using aspect-preserving
  cover scaling.
- Anchor the source crop at its top edge.
- Fade the bottom 70 pixels into the existing dark background.
- Shift the existing controls down 200 pixels and increase window height by
  the same amount. Cheat behavior and bridge communication stay unchanged.

## Verification

- Extend `--self-test` to exercise the palette contract before implementation.
- Build the executable and run `Hotd2RemakeTrainer.exe --self-test`.
- Launch the trainer and capture the rendered window to verify dark background,
  readable text, disabled states, groups, buttons, radios, checkboxes, and
  spinner.
- Verify the embedded header renders from a standalone executable, uses the top
  crop, fades into the dark background, and leaves every control visible.

## Constraints

- No new dependency.
- Header-only layout shift; existing control sizes and behavior stay unchanged.
- Windows 10/11 only, matching the existing application.
