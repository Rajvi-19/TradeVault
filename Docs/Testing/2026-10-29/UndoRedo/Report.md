# Cut and undo/redo verification — 2026-10-29

Build UI 2026.10.29.16. Main Debug application rebuilt with zero warnings/errors.

- Backend: 54 passed, 0 failed (backend.trx).
- Desktop acceptance: 48 passed, 0 failed (desktop-results.md).
- Live self-test: passed persistence/workspace/worksheet checks.
- Cut: whole-record test installs a throwing confirmation callback; Ctrl+X still copies and removes the record, proving confirmation is bypassed.
- Undo/redo: tests cover input/formula restoration, pasted dates as one action, cell cuts, persisted record removal/restoration, draft cut followed by undo/save/redo, unrelated pending inputs, new-edit redo invalidation and reload history reset.
- Storage: restoration checks revision/duplicate rules and leaves existing business validation/payment-history protection in place.
- F1 help: Ctrl+Z and Ctrl+Y are added to registered shortcut list; no on-screen Undo/Redo actions added.

History is memory-only for the active worksheet, cleared on reload, party/tab switch or app close. Ordinary input undo/redo remains unsaved until Save. Undo/redo of stored record removal saves that targeted restoration/removal immediately. No company database/schema changes. Physical input and OS clipboard remain manual device acceptance.
