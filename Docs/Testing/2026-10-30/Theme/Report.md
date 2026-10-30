# Theme verification — 2026-10-30

Build UI 2026.10.30.1, main Debug app updated.

- Build: zero warnings/errors.
- Desktop checks: 52 passed, 0 failed (desktop-results.md).
- Live self-test: passed persistence/workspace/worksheet checks.
- Button: 32x32 moon/sun square follows Save, remains clickable in Saved state, has action tooltip/accessibility name.
- Toggle: existing grids update dark/light without changing input values, revision or undo history.
- Preference: application-local settings survive reload; invalid settings fall back to light; tests use isolated folders.
- Contrast: body/muted/accent text and primary-button text meet 4.5:1 checks.
- Existing native horizontal scrolling, blank-row positions, clipboard and undo/redo acceptance retained.
- Visual: light/dark worksheet screenshots and dark New party inspected. Scrollbars also themed.

No company data, schema or calculation changes. PDF/Excel/printed pages remain white; Windows-native file/printer/message dialogs follow Windows appearance. Physical input and DPI acceptance remain manual.
