# Worksheet interaction verification — 2026-10-29

Build UI 2026.10.29.14. Main Debug application updated.

- Build: zero warnings/errors.
- Desktop acceptance: 45 passed, 0 failed (desktop-results.md).
- Live self-test: passed persistence/workspace/worksheet checks.
- Date regression: WPF date-column clipboard bindings retain all seven editable date fields during full-record copy/paste; calculated dates remain protected.
- Selection regression: Ctrl adds/toggles nonadjacent full rows; a plain click replaces selection.
- Scroll regression: horizontal deltas move the real worksheet ScrollViewer right and left; native touchpad-horizontal-wheel handling and Shift+wheel are wired.
- Visual: toolbar inspected at 1100x720: Receipts, Reminders, Preview / Print, Export Excel, Backups; caption Backups removed.

No financial rules or company data changed. Physical touchpad gestures/driver behavior, OS clipboard and Ctrl-click mouse input remain manual device checks.
