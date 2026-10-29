# Shortcuts, save state and delete confirmation

Build UI 2026.10.28.14. Main application updated in place; no ZIP or working data changes.

| Shortcut | Action |
|---|---|
| Ctrl+S | Save changes |
| Ctrl+N | Go to a new editable record |
| Ctrl+Shift+N | Create a party |
| Ctrl+Shift+Delete | Request deletion of selected record |
| Ctrl+F | Search current worksheet |
| F5 | Reload saved records (existing unsaved-change confirmation applies) |
| Ctrl+L | Choose a party |
| Ctrl+1 / Ctrl+2 / Ctrl+3 | Invoice register / Account details / Debit & credit |
| Ctrl+E | Export Excel |
| Ctrl+P | Preview / print |
| Ctrl+Shift+R | Open receipts |
| F1 | Shortcut help |

Tab/Shift+Tab and Ctrl+C/Ctrl+V remain the grid's existing navigation/copy/paste controls. Plain Delete/Backspace edit text; they do not delete a record. Main-window shortcuts do not apply inside separate modal editors. Save first before export/preview/receipt actions, as with their toolbar buttons. Repeated keydown events are ignored to prevent repeated dialogs.

Save stays in the consistent top-right position. Pending edits produce an enabled blue Save changes (Ctrl+S) button; no pending edits show neutral All changes saved; generated reports show Read-only report. Reserved width prevents navigation shifting as the label changes. Tooltips and F1/Shortcuts provide discovery. The shaded-columns statement was removed; calculated cells remain locked.

Both toolbar deletion and Ctrl+Shift+Delete use the same path. An identified row and party are shown in a modal Yes/No confirmation with No as default. No leaves data unchanged; Yes removes the row from the draft only, and Save commits it. Reload can discard the draft deletion. Invoices with payment history are blocked before removal, with the backend guard retained.

Verification: zero-warning/error builds; 45 acceptance checks passed; live self-test verifies shortcut registration, Ctrl+N/Ctrl+S, save-state labels, cancelled/confirmed deletion, persistence only after Save, and Ctrl+1/2/3 navigation. A backend preflight test verifies payment-history deletion protection. Rendered compact layout inspected. Physical key/mouse interaction and printer acceptance were not performed. No production database was opened by the tests.
