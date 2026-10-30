# Touchpad routing and row numbering — 2026-10-29

Build UI 2026.10.29.15, main Debug executable rebuilt with zero warnings/errors.

- Desktop acceptance: 47 passed, 0 failed (desktop-results.md).
- Live self-test: passed persistence/workspace/worksheet checks.
- Native input: synthetic real-HWND dispatch verifies horizontal mouse-wheel, pointer-wheel, shifted-wheel and legacy line-left/right routing through the installed window hook. Earlier helper-only scrolling test retained.
- Row numbering: delete record 3 of 4, assert record 4 moves to visible row 3, delete first record, then filter; all realized headers stay sequential from 1.
- Company data/schema/accounting rules: unchanged.

Native pan handling added; no physical driver-generated touchpad gesture or HGESTUREINFO delivery is claimed as tested. Reopen the app for physical laptop acceptance.

Implementation follows Microsoft's [horizontal wheel documentation](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-mousehwheel), [pointer horizontal wheel documentation](https://learn.microsoft.com/en-us/windows/win32/inputmsg/wm-pointerhwheel) and [gesture handle lifecycle](https://learn.microsoft.com/en-us/windows/win32/wintouch/wm-gesture).
