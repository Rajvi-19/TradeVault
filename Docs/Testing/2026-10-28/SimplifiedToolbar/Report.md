# Simplified main screen — 2026-10-28

Build UI 2026.10.28.11. Main application updated in place; no ZIP or company database changes.

Removed Recalculate, As of/calendar, Comfortable rows, date-exclusion banner and date-advance prompts. Editing now queues automatic recalculation; row spacing is fixed at the existing compact 28 pixels. Main report views, preview and export use BuildCurrent, including all saved invoices and recorded payments (including future-dated entries). Prepared date remains today; historical service APIs and immutable imported snapshots are preserved. Invoice overdue arithmetic still uses today. Receipts retain their dated allocation/correction workflow.

Restyled Party selector with white rounded border, clearer dropdown arrow, larger selection area and highlighted items. Supports keyboard/type-to-select. Main headings show the selected party with ellipsis/tooltips for long names. Invoice Register no longer displays F1 party name; field keys/party links remain stored, new rows inherit the selection, and the visible first column is Country. No data migration or deletion.

Verification: normal/isolated Desktop builds zero warnings/errors; 45 acceptance checks passed; final main executable live self-test passed. Tests verify automatic difference refresh without Recalculate, party headings/hidden F1, saved future invoices visible in both tabs, party isolation, terms persistence, date validation and keyboard grid navigation. A targeted backend test checks all-record report/export parity and proper prepared-date metadata; passed after correcting the test preview to use Owner privileges. Screenshots inspected at 1100x720 and larger. No physical dropdown/mouse/multi-monitor/printer acceptance is claimed.
