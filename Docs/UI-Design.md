# TradeVault desktop design

## Direction

A compact financial workspace: horizontal worksheet navigation, a narrow navy brand bar, light slate canvas, white spreadsheet surface and one blue primary Save action. Use native WPF controls with shared resource styles; no web runtime, network fonts or third-party UI dependency.

## Tokens and hierarchy

Semantic brush resources live in `App.xaml`: Canvas #F4F6FA, Surface #FFFFFF, Ink #18243B, Muted #64748B, Line #E2E8F0, Accent #2458D3, Sidebar #111E35. `PrimaryButton` and `QuietButton` are component variants. `Ui.cs` provides shared text, icon, badge, card and command construction.

Segoe UI typography: 18 px worksheet titles, 25–29 px sign-in headings, 17 px workspace branding, 13 px body, 10–12 px metadata. Main worksheet margins are 10–12 px; commands share a compact options strip. Keep all workbook field labels intact and wrap long column headings. Default worksheet rows are 28 px, with a 36 px comfortable option.

## Behaviour

- Horizontal tab selection follows the active worksheet. Owner-only Operator reports remains disabled until its real workflow is delivered.
- Save is primary and disabled until an input changes; Ctrl+S saves and Ctrl+N adds a record.
- Saved, unsaved and validation states use text as well as colour. Errors appear inline; deletion and discard require confirmation.
- Worksheets show 100 spare editable rows, replenished as rows are used. Typing promotes a row into a record; untouched blank rows are never saved. There is no Add record or empty-state overlay. Ctrl+N remains an optional keyboard shortcut.
- Date columns accept day-first numeric dates, ISO dates and named months, normalizing to yyyy-MM-dd on commit/paste. Invalid dates remain visible for validation; identifiers stay text. Calculated date columns remain locked.
- Search, rectangular copy/paste, frozen identifiers and locked calculated columns remain available.
- Login supports Enter, labelled username/password fields, keyboard focus, validation feedback and an in-progress state while authentication runs. No remembered password or role-switch bypass.
- Main window minimum is 1000×600 device-independent units; larger spreadsheets scroll horizontally. Login minimum is 760×540. Shared window setup uses native Windows SingleBorderWindow / CanResize chrome on every top-level application window, with no custom WindowChrome. Windows supplies minimize/maximize/restore/close, dragging and sizing. Client-area render PNGs exclude the OS caption. The As of picker uses the same slate/blue border treatment and a calendar button.

## Verification

`--self-test` uses disposable databases and checks real persistence/authentication plus workspace add/save/reload handlers. `--render-ui <folder>` renders empty login and all three worksheets at normal/minimum sizes using isolated disposable data. Neither mode populates the actual company database. Render checks do not replace physical keyboard, high-DPI or screen-reader acceptance testing.
