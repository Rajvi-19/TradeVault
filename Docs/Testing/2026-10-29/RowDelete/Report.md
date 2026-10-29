# Row selection and app-styled delete confirmation — 2026-10-29

Build UI 2026.10.29.1. Main application updated in place; no ZIP or company database changes.

- Delete replaces Ctrl+Shift+Delete as the record-deletion shortcut while the worksheet grid has keyboard focus. Other input controls retain normal Delete behavior; Backspace remains available for text edits in the grid. Help and tooltips updated. Key-repeat suppression remains.
- Clicking a row number selects every visible cell in that one record, including calculated columns and off-screen columns. Cell/range selection and existing clipboard behavior are preserved. Invalid date edits must be corrected or cleared before switching row selection.
- New ConfirmDeleteWindow replaces the native MessageBox. It uses TradeVault caption/colors, record/party information, a neutral explanation, Keep record (default, focused on open, Escape cancels), and explicit red Delete record action. Closing the window cancels. Confirmation remains required for both toolbar and Delete shortcut.
- Cancellation leaves records unchanged. Confirmed removal is still a draft until saved; Reload can discard it. Records with payment history remain protected by service/storage guards. Only a single record is deleted; whole-row selection does not imply bulk deletion.

Verification: zero-warning/error isolated and normal builds; 46 desktop acceptance checks passed (including every-cell row selection); live self-test passed with cancelled/confirmed Delete shortcut and persistence only after Save. Main executable self-test also run. Rendered confirmation dialog inspected at 560x390. No physical keyboard/mouse acceptance claimed. Existing financial rules, workbooks, schema and stored records were not modified.
